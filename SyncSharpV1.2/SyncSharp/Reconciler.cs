﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SyncSharp.DataModel;
using SyncSharp.Storage;
using Microsoft.VisualBasic.FileIO;

namespace SyncSharp.Business
{
	public class SyncSummary
	{
		public int iSrcFileCopy;
		public int iSrcFileDelete;
		public int iSrcFileRename;
		public int iSrcFileOverwrite;
		public int iSrcFolderCreate;
		public int iSrcFolderDelete;

		public int iTgtFileCopy;
		public int iTgtFileDelete;
		public int iTgtFileRename;
		public int iTgtFileOverwrite;
		public int iTgtFolderCreate;
		public int iTgtFolderDelete;

		public DateTime startTime;
		public DateTime endTime;
		public String logFile;

		public SyncSummary()
		{
			iSrcFileCopy = iSrcFileDelete = iSrcFileRename = iSrcFileOverwrite = 0;
			iSrcFolderCreate = iSrcFolderDelete = 0;
			iTgtFileCopy = iTgtFileDelete = iTgtFileRename = iTgtFileOverwrite = 0;
			iTgtFolderCreate = iTgtFolderDelete = 0;
		}
	}

	public class PreviewUnit
	{
		public int intDirtyType;
		public SyncAction sAction;
		public String cleanRelativePath;
		public FileUnit cleanFileUnit;
		public String srcFlag;
		public String tgtFlag;
		public FileUnit srcFile;
		public FileUnit tgtFile;
		public String srcOldRelativePath;
		public String tgtOldRelativePath;
		public Boolean isPathDiff;
	}

	public class Reconciler
	{
		// List of constants.
		const String cFlag = "C";
		const String rFlag = "R";
		const String mFlag = "M";
		const String dFlag = "D";

		FileList _srcList;
		FileList _tgtList;
		String _srcPath;
		String _tgtPath;
		String _taskName;

		TaskSettings _taskSettings;
		SyncSummary _summary;

		// Data Structure for "rename" operation
		CustomDictionary<string, string, FileUnit> _srcRenameList;
		CustomDictionary<string, string, FileUnit> _tgtRenameList;

		// Data Structure for Preview and Sync
		CustomDictionary<string, string, FileUnit> _srcDirtyFilesList;
		CustomDictionary<string, string, FileUnit> _srcCleanFilesList;
		CustomDictionary<string, string, FileUnit> _srcDirtyFoldersList;
		CustomDictionary<string, string, FileUnit> _srcCleanFoldersList;
		CustomDictionary<string, string, FileUnit> _tgtDirtyFilesList;
		CustomDictionary<string, string, FileUnit> _tgtCleanFilesList;
		CustomDictionary<string, string, FileUnit> _tgtDirtyFoldersList;
		CustomDictionary<string, string, FileUnit> _tgtCleanFoldersList;

		// Data Structure for Sync Metadata 
		public CustomDictionary<string, string, FileUnit> _updatedList;

		// Data Structure for Preview Metadata
		public CustomDictionary<string, string, PreviewUnit> _previewFilesList;
		public CustomDictionary<string, string, PreviewUnit> _previewFoldersList;

		public Reconciler(FileList srcList, FileList tgtList, SyncTask task, String metaDataDir)
		{
			_srcList = srcList;
			_tgtList = tgtList;
			_taskSettings = task.Settings;
			_srcPath = task.Source;
			_tgtPath = task.Target;
			_taskName = task.Name;

			_previewFilesList = new CustomDictionary<string, string, PreviewUnit>();
			_previewFoldersList = new CustomDictionary<string, string, PreviewUnit>();
			_updatedList = new CustomDictionary<string, string, FileUnit>();
			_srcRenameList = new CustomDictionary<string, string, FileUnit>();
			_tgtRenameList = new CustomDictionary<string, string, FileUnit>();

			_summary = new SyncSummary();
			_summary.logFile = metaDataDir + @"\" + task.Name + ".log";
		}

		/// <summary>
		/// Perform backup from source replica to target replica.
		/// </summary>
		/// <param name="srcList"></param>
		public void BackupSource(CustomDictionary<string, string, FileUnit> srcList)
		{
			_summary.startTime = DateTime.Now;
			foreach (var myRecord in srcList.Primary)
			{
				String relativePath = myRecord.Key;
				String srcFile; String tgtFile;
				if (relativePath[0] == '\\')
				{ srcFile = _srcPath + relativePath; tgtFile = _tgtPath + relativePath; }
				else
				{
					srcFile = _srcPath + "\\" + relativePath;
					tgtFile = _tgtPath + "\\" + relativePath;
				}
				if (Directory.Exists(srcFile))
				{
					if (!Directory.Exists(tgtFile))
					{
						Directory.CreateDirectory(tgtFile);
						_summary.iTgtFolderCreate++;
						//Logger.WriteEntry("FOLDER ACTION - Create " + tgtFile);
						Logger.WriteLog(Logger.LogType.CreateTGT, null, 0, tgtFile, 0);
					}
				}
				else
				{
					if (!File.Exists(tgtFile))
					{
						checkAndCreateFolder(tgtFile);
						File.Copy(srcFile, tgtFile);
						_summary.iSrcFileCopy++;
						//Logger.WriteEntry("FILE ACTION - Copy " + srcFile + " to " + tgtFile);
						long fileSize = new FileInfo(srcFile).Length;
						Logger.WriteLog(Logger.LogType.CopySRC, srcFile, fileSize, tgtFile, fileSize);
					}
					else
					{
						FileInfo srcFileInfo = new FileInfo(srcFile);
						FileInfo tgtFileInfo = new FileInfo(tgtFile);

						if (srcFileInfo.Name != tgtFileInfo.Name ||
								srcFileInfo.LastWriteTimeUtc != tgtFileInfo.LastWriteTimeUtc
								)
						{
							File.Copy(srcFile, tgtFile, true);
							_summary.iSrcFileCopy++; _summary.iTgtFileOverwrite++;
							//Logger.WriteEntry("FILE ACTION - Copy " + srcFile + " to " + tgtFile);
							long fileSize = new FileInfo(srcFile).Length;
							Logger.WriteLog(Logger.LogType.CopySRC, srcFile, fileSize, tgtFile, fileSize);
						}
					}
				}
			}
			_summary.endTime = DateTime.Now;
			//Logger.CloseLog();
		}

		/// <summary>
		/// Perform restore from target replica to source replica.
		/// </summary>
		/// <param name="srcList"></param>
		public void RestoreSource(CustomDictionary<string, string, FileUnit> srcList)
		{
			_summary.startTime = DateTime.Now;
			foreach (var myRecord in srcList.Primary)
			{
				String relativePath = myRecord.Key;
				String srcFile; String tgtFile;
				if (relativePath[0] == '\\')
				{ srcFile = _srcPath + relativePath; tgtFile = _tgtPath + relativePath; }
				else
				{
					srcFile = _srcPath + "\\" + relativePath;
					tgtFile = _tgtPath + "\\" + relativePath;
				}
				if (Directory.Exists(tgtFile))
				{
					if (!Directory.Exists(srcFile))
					{
						Directory.CreateDirectory(srcFile);
						_summary.iSrcFolderCreate++;
						//Logger.WriteEntry("FOLDER ACTION - Create " + srcFile);
						Logger.WriteLog(Logger.LogType.CreateSRC, srcFile, 0, null, 0);
					}
				}
				else
				{
					if (!File.Exists(srcFile))
					{
						checkAndCreateFolder(srcFile);
						File.Copy(tgtFile, srcFile);
						_summary.iTgtFileCopy++;
						//Logger.WriteEntry("FILE ACTION - Copy " + tgtFile + " to " + srcFile);
						long fileSize = new FileInfo(tgtFile).Length;
						Logger.WriteLog(Logger.LogType.CopyTGT, tgtFile, fileSize, srcFile, fileSize);
					}
					else
					{
						FileInfo srcFileInfo = new FileInfo(srcFile);
						FileInfo tgtFileInfo = new FileInfo(tgtFile);

						if (srcFileInfo.LastWriteTimeUtc != tgtFileInfo.LastWriteTimeUtc)
						{
							File.Copy(tgtFile, srcFile, true);
							_summary.iTgtFileCopy++; _summary.iSrcFileOverwrite++;
							//Logger.WriteEntry("FILE ACTION - Copy " + tgtFile + " to " + srcFile);
							long fileSize = new FileInfo(tgtFile).Length;
							Logger.WriteLog(Logger.LogType.CopyTGT, tgtFile, fileSize, srcFile, fileSize);
						}
					}
				}
			}
			_summary.endTime = DateTime.Now;
			//Logger.CloseLog();
		}

		public void SyncWithMeta()
		{
			Sync();
		}

		/// <summary>
		/// Perform sync preview between source replica and target replica.
		/// </summary>
		public void Preview()
		{
			FileUnit srcFile; FileUnit tgtFile;

			GetFilesFoldersLists();
			CheckRename();

			foreach (var mySrcRecord in _srcDirtyFilesList.PriSub)
			{
				String relativePath = mySrcRecord.Key;
				String srcSecondKey = mySrcRecord.Value;
				String srcFlag = "" + srcSecondKey[0];
				srcFile = _srcDirtyFilesList.getByPrimary(relativePath);

				if (srcFlag.Equals(dFlag) && _srcRenameList.containsSecKey(relativePath))
					continue;

				if (_tgtCleanFilesList.containsPriKey(relativePath))
					PreviewSrcDirtyTgtClean(relativePath, srcFlag);

				else if (!_tgtDirtyFilesList.containsPriKey(relativePath))
				{
					if (srcFlag.Equals(cFlag))
					{
						if (!_srcRenameList.containsPriKey(relativePath))
							PreviewSrcDirtyTgtClean(relativePath, srcFlag);

						else
						{
							String delRelativePath = _srcRenameList.PriSub[relativePath];
							if (_tgtCleanFilesList.containsPriKey(delRelativePath))
								PreviewSrcDirtyTgtClean(relativePath, srcFlag);

							else if (_tgtDirtyFilesList.containsPriKey(delRelativePath))
								PreviewSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, delRelativePath, "", null);
						}
					}
				}
				else if (_tgtDirtyFilesList.containsPriKey(relativePath))
				{
					string tgtSecondKey = _tgtDirtyFilesList.PriSub[relativePath];
					String tgtFlag = "" + tgtSecondKey[0];
					String tgtHashCode = tgtSecondKey.Substring(2);
					tgtFile = _tgtDirtyFilesList.getByPrimary(relativePath);

					if (tgtFlag.Equals(dFlag) && _tgtRenameList.containsSecKey(relativePath))
						PreviewSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, relativePath, tgtFlag, tgtFile);

					else if (srcFlag.Equals(cFlag) && _srcRenameList.containsPriKey(relativePath))
						PreviewSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, relativePath, tgtFlag, tgtFile);

					else
					{
						PreviewSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, relativePath, tgtFlag, tgtFile);
					}
				}
			}

			foreach (var myTgtRecord in _tgtDirtyFilesList.PriSub)
			{
				String relativePath = myTgtRecord.Key;
				String tgtSecondKey = myTgtRecord.Value;
				String tgtFlag = "" + tgtSecondKey[0];

				if (tgtFlag.Equals(dFlag) && _tgtRenameList.containsSecKey(relativePath))
					continue;
				PreviewSrcCleanTgtDirty(relativePath, tgtFlag);
			}

			foreach (var myRecord in _srcCleanFilesList.PriSub)
			{
				String relativePath = myRecord.Key;
				PreviewUnit preview = new PreviewUnit();
				preview.intDirtyType = 3;
				preview.sAction = SyncAction.NoAction;
				preview.cleanRelativePath = relativePath;
				preview.cleanFileUnit = _srcCleanFilesList.getByPrimary(relativePath);
				_previewFilesList.add(relativePath, relativePath, preview);
			}

			PreviewFoldersCleanup();
		}

		/// <summary>
		/// Perform sync between source replica and target replica based on the preview results.
		/// </summary>
		public void SyncPreview()
		{
			foreach (var myPreviewRecord in _previewFilesList.PriSub)
			{
				String srcRelativePath = myPreviewRecord.Key;
				String tgtRelativePath = myPreviewRecord.Value;
				PreviewUnit preview = _previewFilesList.getByPrimary(srcRelativePath);

				switch (preview.intDirtyType)
				{
					case 0:
						SyncPreviewSrcDirtyTgtClean(srcRelativePath, tgtRelativePath, preview);
						break;
					case 1:
						SyncPreviewSrcCleanTgtDirty(srcRelativePath, tgtRelativePath, preview);
						break;
					case 2:
						SyncPreviewSrcDirtyTgtDirty(srcRelativePath, tgtRelativePath, preview);
						break;
					case 3:
						_updatedList.add(preview.cleanRelativePath, preview.cleanFileUnit.Hash, preview.cleanFileUnit);
						break;
				}
			}

			foreach (var myPreviewRecord in _previewFoldersList.PriSub)
			{
				String srcRelativePath = myPreviewRecord.Key;
				String tgtRelativePath = myPreviewRecord.Value;
				PreviewUnit preview = _previewFoldersList.getByPrimary(srcRelativePath);

				SyncPreviewFoldersCleanup(srcRelativePath, tgtRelativePath, preview);
			}
		}


		/// <summary>
		/// Perform sync between source replica and target replica.
		/// </summar>y
		public void Sync()
		{
			_summary.startTime = DateTime.Now;
			FileUnit srcFile; FileUnit tgtFile;

			GetFilesFoldersLists();
			CheckRename();

			foreach (var mySrcRecord in _srcDirtyFilesList.PriSub)
			{
				String relativePath = mySrcRecord.Key;
				String srcSecondKey = mySrcRecord.Value;
				String srcFlag = "" + srcSecondKey[0];
				srcFile = _srcDirtyFilesList.getByPrimary(relativePath);

				if (srcFlag.Equals(dFlag) && _srcRenameList.containsSecKey(relativePath))
					continue;

				if (_tgtCleanFilesList.containsPriKey(relativePath))
					SyncSrcDirtyTgtClean(relativePath, srcFlag);

				else if (!_tgtDirtyFilesList.containsPriKey(relativePath))
				{
					if (srcFlag.Equals(cFlag))
					{
						if (!_srcRenameList.containsPriKey(relativePath))
							SyncSrcDirtyTgtClean(relativePath, srcFlag);

						else
						{
							String delRelativePath = _srcRenameList.PriSub[relativePath];
							if (_tgtCleanFilesList.containsPriKey(delRelativePath))
								SyncSrcDirtyTgtClean(relativePath, srcFlag);

							else if (_tgtDirtyFilesList.containsPriKey(delRelativePath))
								SyncSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, delRelativePath, "", null);
						}
					}
				}
				else if (_tgtDirtyFilesList.containsPriKey(relativePath))
				{
					string tgtSecondKey = _tgtDirtyFilesList.PriSub[relativePath];
					String tgtFlag = "" + tgtSecondKey[0];
					String tgtHashCode = tgtSecondKey.Substring(2);
					tgtFile = _tgtDirtyFilesList.getByPrimary(relativePath);

					if (tgtFlag.Equals(dFlag) && _tgtRenameList.containsSecKey(relativePath))
						SyncSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, relativePath, tgtFlag, tgtFile);

					else if (srcFlag.Equals(cFlag) && _srcRenameList.containsPriKey(relativePath))
						SyncSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, relativePath, tgtFlag, tgtFile);

					else
					{
						SyncSrcDirtyTgtDirty(relativePath, srcFlag, srcFile, relativePath, tgtFlag, tgtFile);
					}
				}
			}

			foreach (var myTgtRecord in _tgtDirtyFilesList.PriSub)
			{
				String relativePath = myTgtRecord.Key;
				String tgtSecondKey = myTgtRecord.Value;
				String tgtFlag = "" + tgtSecondKey[0];

				if (tgtFlag.Equals(dFlag) && _tgtRenameList.containsSecKey(relativePath))
					continue;
				SyncSrcCleanTgtDirty(relativePath, tgtFlag);
			}

			foreach (var myRecord in _srcCleanFilesList.PriSub)
			{
				String relativePath = myRecord.Key;
				String secondKey = myRecord.Value;
				FileUnit fileMeta = _srcCleanFilesList.Primary[relativePath];
				_updatedList.add(relativePath, secondKey, fileMeta);
			}

			SyncFoldersCleanup();

			_summary.endTime = DateTime.Now;
			//Logger.CloseLog();
		}

		/// <summary>
		/// Get the list of dirty and clean files and folders.
		/// </summary>
		private void GetFilesFoldersLists()
		{
			_srcDirtyFilesList = _srcList.DirtyFiles;
			_srcCleanFilesList = _srcList.CleanFiles;
			_srcDirtyFoldersList = _srcList.DirtyDirs;
			_srcCleanFoldersList = _srcList.CleanDirs;
			_tgtDirtyFilesList = _tgtList.DirtyFiles;
			_tgtCleanFilesList = _tgtList.CleanFiles;
			_tgtDirtyFoldersList = _tgtList.DirtyDirs;
			_tgtCleanFoldersList = _tgtList.CleanDirs;
		}

		/// <summary>
		/// Check the renamed or moved files on the replicas.
		/// </summary>
		private void CheckRename()
		{
			foreach (var mySrcRecord in _srcDirtyFilesList.PriSub)
			{
				String relativePath = mySrcRecord.Key;
				String srcSecondKey = mySrcRecord.Value;
				String srcFlag = "" + srcSecondKey[0];
				String srcHashCode = srcSecondKey.Substring(2);

				if (srcFlag.Equals(cFlag))
				{
					if (_srcDirtyFilesList.containsSecKey(dFlag + "-" + srcHashCode) && !_srcRenameList.containsPriKey(relativePath))
					{
						List<String> delFiles = _srcDirtyFilesList.getBySecondary(dFlag + "-" + srcHashCode);
						foreach (String myFile in delFiles)
						{
							if (!_srcRenameList.containsSecKey(myFile))
							{
								_srcRenameList.add(relativePath, myFile, _srcDirtyFilesList.getByPrimary(relativePath));
								break;
							}
						}
					}
				}
			}

			foreach (var myTgtRecord in _tgtDirtyFilesList.PriSub)
			{
				String relativePath = myTgtRecord.Key;
				String tgtSecondKey = myTgtRecord.Value;
				String tgtFlag = "" + tgtSecondKey[0];
				String tgtHashCode = tgtSecondKey.Substring(2);

				if (tgtFlag.Equals(cFlag))
				{
					if (_tgtDirtyFilesList.containsSecKey(dFlag + "-" + tgtHashCode) && !_tgtRenameList.containsPriKey(relativePath))
					{
						List<String> delFiles = _tgtDirtyFilesList.getBySecondary("D-" + tgtHashCode);
						foreach (String myFile in delFiles)
						{
							if (!_tgtRenameList.containsSecKey(myFile))
							{
								_tgtRenameList.add(relativePath, myFile, _tgtDirtyFilesList.getByPrimary(relativePath));
								break;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Perform preview sync between source dirty file and target clean file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="relativePath"></param>
		/// <param name="srcFlag"></param>
		private void PreviewSrcDirtyTgtClean(String relativePath, String srcFlag)
		{
			PreviewUnit preview = new PreviewUnit();
			preview.intDirtyType = 0;

			switch (srcFlag)
			{
				case cFlag:
					{
						preview.srcFlag = cFlag;

						if (!_srcRenameList.containsPriKey(relativePath))
						{
							preview.tgtFlag = dFlag;
							preview.sAction = SyncAction.CopyFileToTarget;
							preview.cleanRelativePath = "";
							preview.cleanFileUnit = null;
							_previewFilesList.add(relativePath, relativePath, preview);
						}
						else
						{
							preview.tgtFlag = "";
							String delRelativePath = _srcRenameList.PriSub[relativePath];
							preview.sAction = SyncAction.RenameTargetFile;
							preview.cleanRelativePath = delRelativePath;
							preview.cleanFileUnit = _tgtCleanFilesList.getByPrimary(delRelativePath);
							_previewFilesList.add(relativePath, delRelativePath, preview);
							_tgtCleanFilesList.removeByPrimary(delRelativePath);
						}
					}
					break;
				case mFlag:
					{
						preview.sAction = SyncAction.CopyFileToTarget;
						preview.cleanRelativePath = relativePath;
						preview.cleanFileUnit = _tgtCleanFilesList.getByPrimary(relativePath);
						preview.srcFlag = mFlag;
						preview.tgtFlag = "";
						_previewFilesList.add(relativePath, relativePath, preview);
						_tgtCleanFilesList.removeByPrimary(relativePath);
					}
					break;
				case dFlag:
					{
						preview.sAction = SyncAction.DeleteTargetFile;
						preview.cleanRelativePath = relativePath;
						preview.cleanFileUnit = _tgtCleanFilesList.getByPrimary(relativePath);
						preview.srcFlag = dFlag;
						preview.tgtFlag = "";
						_previewFilesList.add(relativePath, relativePath, preview);
						_tgtCleanFilesList.removeByPrimary(relativePath);
					}
					break;
			}
		}

		/// <summary>
		/// Perform sync action based on preview result between source dirty file and target clean file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="srcRelativePath"></param>
		/// <param name="tgtRelativePath"></param>
		/// <param name="preview"></param>
		private void SyncPreviewSrcDirtyTgtClean(String srcRelativePath, String tgtRelativePath, PreviewUnit preview)
		{
			switch (preview.sAction)
			{
				case SyncAction.CopyFileToTarget:
					{
						checkAndCreateFolder(_tgtPath + srcRelativePath);
						File.Copy(_srcPath + srcRelativePath, _tgtPath + srcRelativePath, true);
						FileUnit myFile = new FileUnit(_srcPath + srcRelativePath);
						myFile.Hash = Utility.computeMyHash(myFile);
						_updatedList.add(srcRelativePath, myFile.Hash, myFile);
					}
					break;
				case SyncAction.RenameTargetFile:
					{
						checkAndCreateFolder(_tgtPath + srcRelativePath);
						File.Move(_tgtPath + preview.cleanRelativePath, _tgtPath + srcRelativePath);
						FileUnit myFile = new FileUnit(_tgtPath + srcRelativePath);
						myFile.Hash = Utility.computeMyHash(myFile);
						_updatedList.add(srcRelativePath, myFile.Hash, myFile);
					}
					break;
				case SyncAction.DeleteTargetFile:
					{
						File.Delete(_tgtPath + srcRelativePath);
					}
					break;
				case SyncAction.NoAction:
					{
						_updatedList.add(preview.cleanRelativePath, preview.cleanFileUnit.Hash, preview.cleanFileUnit);
					}
					break;

			}
		}


		/// <summary>
		/// Perform preview sync between source clean file and target dirty file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="relativePath"></param>
		/// <param name="tgtFlag"></param>
		private void PreviewSrcCleanTgtDirty(String relativePath, String tgtFlag)
		{
			PreviewUnit preview = new PreviewUnit();
			preview.intDirtyType = 1;

			switch (tgtFlag)
			{
				case cFlag:
					{
						if (!_tgtRenameList.containsPriKey(relativePath))
						{
							preview.sAction = SyncAction.CopyFileToSource;
							preview.cleanRelativePath = "";
							preview.cleanFileUnit = null;
							preview.srcFlag = dFlag; // added in deleted flag for first time sync
							preview.tgtFlag = cFlag;
							_previewFilesList.add(relativePath, relativePath, preview);
						}
						else
						{
							String delRelativePath = _tgtRenameList.PriSub[relativePath];
							if (_srcCleanFilesList.containsPriKey(delRelativePath))
							{
								preview.sAction = SyncAction.RenameSourceFile;
								preview.cleanRelativePath = delRelativePath;
								preview.cleanFileUnit = _srcCleanFilesList.getByPrimary(delRelativePath);
								preview.srcFlag = "";
								preview.tgtFlag = cFlag;
								_previewFilesList.add(delRelativePath, relativePath, preview);
								_srcCleanFilesList.removeByPrimary(delRelativePath);
							}
						}
					}
					break;
				case mFlag:
					{
						preview.sAction = SyncAction.CopyFileToSource;
						preview.cleanRelativePath = relativePath;
						preview.cleanFileUnit = _srcCleanFilesList.getByPrimary(relativePath);
						preview.srcFlag = "";
						preview.tgtFlag = mFlag;
						_previewFilesList.add(relativePath, relativePath, preview);
						_srcCleanFilesList.removeByPrimary(relativePath);
					}
					break;
				case dFlag:
					{
						preview.sAction = SyncAction.DeleteSourceFile;
						preview.cleanRelativePath = relativePath;
						preview.cleanFileUnit = _srcCleanFilesList.getByPrimary(relativePath);
						preview.srcFlag = "";
						preview.tgtFlag = dFlag;
						_previewFilesList.add(relativePath, relativePath, preview);
						_srcCleanFilesList.removeByPrimary(relativePath);
					}
					break;
			}
		}


		/// <summary>
		/// Perform sync action based on preview result between source clean file and target dirty file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="srcRelativePath"></param>
		/// <param name="tgtRelativePath"></param>
		/// <param name="preview"></param>
		private void SyncPreviewSrcCleanTgtDirty(String srcRelativePath, String tgtRelativePath, PreviewUnit preview)
		{
			switch (preview.sAction)
			{
				case SyncAction.CopyFileToSource:
					{
						checkAndCreateFolder(_srcPath + tgtRelativePath);
						File.Copy(_tgtPath + tgtRelativePath, _srcPath + tgtRelativePath, true);
						FileUnit myFile = new FileUnit(_tgtPath + tgtRelativePath);
						myFile.Hash = Utility.computeMyHash(myFile);
						_updatedList.add(tgtRelativePath, myFile.Hash, myFile);
					}
					break;
				case SyncAction.RenameSourceFile:
					{
						checkAndCreateFolder(_srcPath + tgtRelativePath);
						File.Move(_srcPath + preview.cleanRelativePath, _srcPath + tgtRelativePath);
						FileUnit myFile = new FileUnit(_srcPath + tgtRelativePath);
						myFile.Hash = Utility.computeMyHash(myFile);
						_updatedList.add(tgtRelativePath, myFile.Hash, myFile);
					}
					break;
				case SyncAction.DeleteSourceFile:
					{
						File.Delete(_srcPath + tgtRelativePath);
					}
					break;
				case SyncAction.NoAction:
					{
						_updatedList.add(preview.cleanRelativePath, preview.cleanFileUnit.Hash, preview.cleanFileUnit);
					}
					break;
			}
		}

		/// <summary>
		/// Perform preview sync between source dirty file and target dirty file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="srcRelativePath"></param>
		/// <param name="srcFlag"></param>
		/// <param name="srcFile"></param>
		/// <param name="tgtRelativePath"></param>
		/// <param name="tgtFlag"></param>
		/// <param name="tgtFile"></param>
		private void PreviewSrcDirtyTgtDirty(String srcRelativePath, String srcFlag, FileUnit srcFile, String tgtRelativePath, String tgtFlag, FileUnit tgtFile)
		{
			PreviewUnit preview = new PreviewUnit();
			preview.intDirtyType = 2;

			if (_tgtDirtyFilesList.containsPriKey(tgtRelativePath))
			{
				if (_tgtRenameList.containsSecKey(tgtRelativePath) && !_tgtRenameList.containsPriKey(srcRelativePath))
				{
					preview.cleanRelativePath = tgtRelativePath;
					preview.cleanFileUnit = _tgtDirtyFilesList.getByPrimary(tgtRelativePath);

					tgtFlag = cFlag;
					List<String> lstFiles = _tgtRenameList.SubPri[tgtRelativePath];
					tgtRelativePath = lstFiles[0];
					tgtFile = _tgtRenameList.getByPrimary(tgtRelativePath);
				}
				else if (!_tgtDirtyFilesList.containsPriKey(srcRelativePath))
				{
					tgtFlag = "" + _tgtDirtyFilesList.PriSub[tgtRelativePath][0];

					preview.cleanRelativePath = tgtRelativePath;
					preview.cleanFileUnit = tgtFile = _tgtDirtyFilesList.getByPrimary(tgtRelativePath);
				}
				else if (srcFlag.Equals(cFlag) && _srcRenameList.containsPriKey(srcRelativePath))
				{
					String cleanPath = _srcRenameList.PriSub[srcRelativePath];
					preview.cleanRelativePath = cleanPath;
					preview.cleanFileUnit = _srcDirtyFilesList.getByPrimary(cleanPath);
				}
				else
				{
					preview.cleanRelativePath = srcRelativePath;
					preview.cleanFileUnit = srcFile;
				}

			}

			int iSrcSlash = srcRelativePath.LastIndexOf('\\');
			int iTgtSlash = tgtRelativePath.LastIndexOf('\\');

			String srcFilePath = "";
			if (iSrcSlash > 0) { srcFilePath = srcRelativePath.Substring(0, iSrcSlash+1); }

			String tgtFilePath = "";
			if (iTgtSlash > 0) { tgtFilePath = tgtRelativePath.Substring(0, iTgtSlash+1); }

			preview.srcFlag = srcFlag; preview.tgtFlag = tgtFlag;
			preview.srcFile = srcFile; preview.tgtFile = tgtFile;
			preview.sAction = SyncAction.NoAction;

			if (!srcFilePath.Equals(tgtFilePath))
			{
				preview.isPathDiff = true;

				if (_taskSettings.FolderConflict == TaskSettings.ConflictFolderAction.KeepSourceName)
				{
					if (!tgtFlag.Equals(dFlag))
					{
						if (srcFlag.Equals(dFlag) && _srcDirtyFoldersList.containsSecKey(dFlag + "-" + srcFilePath))
							preview.sAction = checkConflicts(srcFile, tgtFile, srcFlag, tgtFlag);

						else
						{
							if (_srcDirtyFoldersList.containsPriKey(tgtFilePath))
								_srcDirtyFoldersList.removeByPrimary(tgtFilePath);
							if (_tgtDirtyFoldersList.containsPriKey(tgtFilePath))
							{
								_tgtCleanFoldersList.add(tgtFilePath, tgtFilePath, _tgtDirtyFoldersList.getByPrimary(tgtFilePath));
								_tgtDirtyFoldersList.removeByPrimary(tgtFilePath);
							}
						}
					}
					if (preview.sAction == SyncAction.NoAction) preview.sAction = SyncAction.RenameTargetFile;
				}
				else
				{
					if (!srcFlag.Equals(dFlag))
					{
						if (tgtFlag.Equals(dFlag) && _tgtDirtyFoldersList.containsSecKey(dFlag + "-" + tgtFilePath))
							preview.sAction = checkConflicts(srcFile, tgtFile, srcFlag, tgtFlag);

						else
						{
							if (_srcDirtyFoldersList.containsPriKey(srcFilePath))
							{
								_srcCleanFoldersList.add(srcFilePath, srcFilePath, _srcDirtyFoldersList.getByPrimary(srcFilePath));
								_srcDirtyFoldersList.removeByPrimary(srcFilePath);
							}
							if (_tgtDirtyFoldersList.containsPriKey(srcFilePath))
								_tgtDirtyFoldersList.removeByPrimary(srcFilePath);
						}
					}
					if (preview.sAction == SyncAction.NoAction) preview.sAction = SyncAction.RenameSourceFile;

				}

			}
			else
			{
				preview.isPathDiff = false;

				if (srcFlag.Equals(cFlag) && _srcRenameList.containsPriKey(srcRelativePath))
				{
					String delSrcFile = _srcRenameList.PriSub[srcRelativePath];
					preview.srcOldRelativePath = delSrcFile;

					if (_tgtCleanFilesList.containsPriKey(delSrcFile)) _tgtCleanFilesList.removeByPrimary(delSrcFile);
					if (tgtFlag.Equals(cFlag) && _tgtRenameList.containsPriKey(srcRelativePath))
					{
						String delTgtFile = _tgtRenameList.PriSub[srcRelativePath];
						preview.tgtOldRelativePath = delTgtFile;
						if (_srcCleanFilesList.containsPriKey(delTgtFile)) _srcCleanFilesList.removeByPrimary(delTgtFile);
					}
				}
				else if (srcFlag.Equals(cFlag) && _tgtRenameList.containsPriKey(srcRelativePath))
				{
					String delTgtFile = _tgtRenameList.PriSub[srcRelativePath];
					preview.tgtOldRelativePath = delTgtFile;
					if (_srcCleanFilesList.containsPriKey(delTgtFile)) _srcCleanFilesList.removeByPrimary(delTgtFile);
				}

				preview.sAction = checkConflicts(srcFile, tgtFile, srcFlag, tgtFlag);
			}
			_tgtDirtyFilesList.removeByPrimary(tgtRelativePath);
			_previewFilesList.add(srcRelativePath, tgtRelativePath, preview);
		}


		/// <summary>
		///  Perform sync action based on preview result between source dirty file and target dirty file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="srcRelativePath"></param>
		/// <param name="tgtRelativePath"></param>
		/// <param name="preview"></param>
		private void SyncPreviewSrcDirtyTgtDirty(String srcRelativePath, String tgtRelativePath, PreviewUnit preview)
		{
			int iSrcSlash = srcRelativePath.LastIndexOf('\\');
			int iTgtSlash = tgtRelativePath.LastIndexOf('\\');

			String srcFilePath = "";
			if (iSrcSlash > 0) { srcFilePath = srcRelativePath.Substring(0, iSrcSlash+1); }

			String tgtFilePath = "";
			if (iTgtSlash > 0) { tgtFilePath = tgtRelativePath.Substring(0, iTgtSlash+1); }

			if (!srcFilePath.Equals(tgtFilePath))
			{

				if (_taskSettings.FolderConflict == TaskSettings.ConflictFolderAction.KeepSourceName)
				{
					if (!preview.tgtFlag.Equals(dFlag))
					{
						if (preview.srcFlag.Equals(dFlag))
						{
							PreviewUnit previewFolder = _previewFoldersList.getByPrimary(srcFilePath);
							if (previewFolder.srcFlag.Equals(dFlag)) srcFilePath = tgtFilePath;
						}
						else
						{
							checkAndCreateFolder(_tgtPath + srcRelativePath);
							File.Move(_tgtPath + tgtRelativePath, _tgtPath + srcFilePath + tgtRelativePath.Substring(iTgtSlash));
							DirectoryInfo dir = new DirectoryInfo(_tgtPath + tgtFilePath);

							int iFileCount = dir.GetFiles().Length;
							int iDirCount = dir.GetDirectories().Length;

							if (iFileCount == 0 && iDirCount == 0)
							{
								if (_previewFoldersList.containsPriKey(tgtFilePath))
								{
									PreviewUnit previewFolder = _previewFoldersList.getByPrimary(tgtFilePath);
									previewFolder.srcFlag = dFlag;
									previewFolder.tgtFlag = dFlag;
									_previewFoldersList.removeByPrimary(tgtFilePath);
									_previewFoldersList.add(tgtFilePath, tgtFilePath, previewFolder);
								}
							}
						}
					}
					tgtFilePath = srcFilePath;
				}
				else
				{
					if (!preview.srcFlag.Equals(dFlag))
					{
						if (preview.tgtFlag.Equals(dFlag))
						{
							PreviewUnit previewFolder = _previewFoldersList.getByPrimary(tgtFilePath);
							if (previewFolder.tgtFlag.Equals(dFlag)) tgtFilePath = srcFilePath;
						}
						else
						{
							checkAndCreateFolder(_srcPath + tgtRelativePath);
							File.Move(_srcPath + srcRelativePath, _srcPath + tgtFilePath + srcRelativePath.Substring(iTgtSlash));
							DirectoryInfo dir = new DirectoryInfo(_srcPath + srcFilePath);

							int iFileCount = dir.GetFiles().Length;
							int iDirCount = dir.GetDirectories().Length;

							if (iFileCount == 0 && iDirCount == 0)
							{
								if (_previewFoldersList.containsPriKey(srcFilePath))
								{
									PreviewUnit previewFolder = _previewFoldersList.getByPrimary(srcFilePath);
									previewFolder.srcFlag = dFlag;
									previewFolder.tgtFlag = dFlag;
									_previewFoldersList.removeByPrimary(srcFilePath);
									_previewFoldersList.add(srcFilePath, srcFilePath, previewFolder);
								}
							}
						}
					}
					srcFilePath = tgtFilePath;
				}
				executeSyncAction(preview.srcFile, preview.tgtFile, preview.srcFlag, preview.tgtFlag,
								_srcPath + srcFilePath, _tgtPath + tgtFilePath);
			}
			else
			{
				executeSyncAction(preview.srcFile, preview.tgtFile, preview.srcFlag, preview.tgtFlag,
						_srcPath + srcFilePath, _tgtPath + srcFilePath);

				if (preview.srcFlag.Equals(cFlag) && preview.srcOldRelativePath != null)
				{
					if (File.Exists(_tgtPath + preview.srcOldRelativePath)) File.Delete(_tgtPath + preview.srcOldRelativePath);

					if (preview.tgtFlag.Equals(cFlag) && preview.tgtOldRelativePath != null)
						if (File.Exists(_srcPath + preview.tgtOldRelativePath))
							File.Delete(_srcPath + preview.tgtOldRelativePath);
				}
				else if (preview.srcFlag.Equals(cFlag) && preview.tgtOldRelativePath != null)
					if (File.Exists(_srcPath + preview.tgtOldRelativePath))
						File.Delete(_srcPath + preview.tgtOldRelativePath);
			}


		}


		/// <summary>
		/// Preview folder cleanup after synchronization.
		/// </summary>
		private void PreviewFoldersCleanup()
		{
			foreach (var mySrcRecord in _srcDirtyFoldersList.PriSub)
			{
				String relativePath = mySrcRecord.Key;
				String srcSecondKey = mySrcRecord.Value;
				String srcFlag = "" + srcSecondKey[0];
				FileUnit srcFile = _srcDirtyFoldersList.getByPrimary(relativePath);

				PreviewUnit preview = new PreviewUnit();

				if (srcFlag.Equals(cFlag))
				{
					preview.srcFlag = cFlag;
					preview.tgtFlag = dFlag;
					preview.srcFile = srcFile;

					if (_tgtDirtyFoldersList.containsSecKey(cFlag + "-" + relativePath))
					{
						preview.sAction = SyncAction.NoAction;
						_tgtDirtyFoldersList.removeByPrimary(relativePath);
					}
					else
						preview.sAction = SyncAction.CreateTargetDir;

				}
				else if (srcFlag.Equals(dFlag))
				{
					preview.cleanRelativePath = relativePath;
					preview.cleanFileUnit = srcFile;
					preview.srcFlag = dFlag;

					if (_tgtDirtyFoldersList.containsSecKey(dFlag + "-" + relativePath))
					{
						preview.sAction = SyncAction.DeleteBothDir;
						preview.tgtFlag = dFlag;
						_tgtDirtyFoldersList.removeByPrimary(relativePath);
					}
					else if (_tgtCleanFoldersList.containsPriKey(relativePath))
					{
						if (!Directory.Exists(_tgtPath + relativePath))
							preview.tgtFlag = dFlag;

						else
							preview.tgtFlag = "";

						preview.sAction = SyncAction.DeleteTargetDir;
						_tgtCleanFoldersList.removeByPrimary(relativePath);
					}
				}
				_previewFoldersList.add(relativePath, relativePath, preview);

			}

			foreach (var myTgtRecord in _tgtDirtyFoldersList.PriSub)
			{
				PreviewUnit preview = new PreviewUnit();

				String relativePath = myTgtRecord.Key;
				String tgtSecondKey = myTgtRecord.Value;
				String tgtFlag = "" + tgtSecondKey[0];
				FileUnit tgtFile = _tgtDirtyFoldersList.getByPrimary(relativePath);

				if (tgtFlag.Equals(cFlag))
				{
					preview.sAction = SyncAction.CreateSourceDir;
					preview.srcFlag = dFlag;
					preview.tgtFlag = cFlag;
					preview.tgtFile = tgtFile;
				}
				else if (tgtFlag.Equals(dFlag))
				{
					preview.cleanRelativePath = relativePath;
					preview.cleanFileUnit = tgtFile;
					preview.srcFlag = "";
					preview.tgtFlag = dFlag;
					preview.sAction = SyncAction.DeleteSourceDir;
					_srcCleanFoldersList.removeByPrimary(relativePath);
				}
				_previewFoldersList.add(relativePath, relativePath, preview);
			}

			foreach (var myRecord in _srcCleanFoldersList.PriSub)
			{
				PreviewUnit preview = new PreviewUnit();
				preview.sAction = SyncAction.NoAction;
				preview.cleanRelativePath = myRecord.Key;
				preview.srcFile = preview.cleanFileUnit = _srcCleanFoldersList.Primary[myRecord.Key];
				_previewFoldersList.add(myRecord.Key, myRecord.Key, preview);
				if (_tgtCleanFoldersList.containsPriKey(myRecord.Key))
					_tgtCleanFoldersList.removeByPrimary(myRecord.Key);
			}

			foreach (var myRecord in _tgtCleanFoldersList.PriSub)
			{
				PreviewUnit preview = new PreviewUnit();
				preview.sAction = SyncAction.NoAction;
				preview.cleanRelativePath = myRecord.Key;
				preview.srcFile = preview.cleanFileUnit = _srcCleanFoldersList.Primary[myRecord.Key];
				if (!_previewFoldersList.containsPriKey(myRecord.Key))
					_previewFoldersList.add(myRecord.Key, myRecord.Key, preview);
			}
		}


		/// <summary>
		/// Perform sync folder based on the preview result
		/// </summary>
		/// <param name="srcRelativePath"></param>
		/// <param name="tgtRelativePath"></param>
		/// <param name="preview"></param>
		private void SyncPreviewFoldersCleanup(String srcRelativePath, String tgtRelativePath, PreviewUnit preview)
		{
			switch (preview.sAction)
			{
				case SyncAction.CreateSourceDir:
					{
						if (!Directory.Exists(_srcPath + srcRelativePath))
							Directory.CreateDirectory(_srcPath + srcRelativePath);
						if (!_updatedList.containsPriKey(srcRelativePath))
							_updatedList.add(srcRelativePath, srcRelativePath, preview.tgtFile);
					}
					break;
				case SyncAction.CreateTargetDir:
					{
						if (!Directory.Exists(_tgtPath + tgtRelativePath))
							Directory.CreateDirectory(_tgtPath + tgtRelativePath);
						if (!_updatedList.containsPriKey(tgtRelativePath))
							_updatedList.add(srcRelativePath, tgtRelativePath, preview.srcFile);
					}
					break;
				case SyncAction.DeleteBothDir:
					{
						if (Directory.Exists(_srcPath + srcRelativePath))
							Directory.Delete(_srcPath + srcRelativePath);
						if (Directory.Exists(_tgtPath + tgtRelativePath))
							Directory.Delete(_tgtPath + tgtRelativePath);
					}
					break;
				case SyncAction.DeleteSourceDir:
					{
						if (Directory.Exists(_srcPath + srcRelativePath))
							Directory.Delete(_srcPath + srcRelativePath);
					}
					break;
				case SyncAction.DeleteTargetDir:
					{
						if (Directory.Exists(_tgtPath + tgtRelativePath))
							Directory.Delete(_tgtPath + tgtRelativePath);
					}
					break;
				case SyncAction.NoAction:
					{
						if (Directory.Exists(_srcPath + srcRelativePath) && Directory.Exists(_tgtPath + tgtRelativePath))
							_updatedList.add(srcRelativePath, srcRelativePath, preview.srcFile);
						else
						{
							if (Directory.Exists(_srcPath + srcRelativePath))
								Directory.Delete(_srcPath + srcRelativePath);
							if (Directory.Exists(_tgtPath + tgtRelativePath))
								Directory.Delete(_tgtPath + tgtRelativePath);
						}
					}
					break;
			}
		}


		/// <summary>
		/// Perform sync between source dirty file and target clean file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="relativePath"></param>
		/// <param name="srcFlag"></param>
		private void SyncSrcDirtyTgtClean(String relativePath, String srcFlag)
		{
			switch (srcFlag)
			{
				case cFlag:
					{
						if (!_srcRenameList.containsPriKey(relativePath))
						{
							FileUnit fileMeta = new FileUnit(_srcPath + relativePath);
							String hashCode = Utility.computeMyHash(fileMeta);
							fileMeta.Hash = hashCode;
							checkAndCreateFolder(_tgtPath + relativePath);
							File.Copy(_srcPath + relativePath, _tgtPath + relativePath);
							_updatedList.add(relativePath, hashCode, fileMeta);
							_summary.iSrcFileCopy++;
							//Logger.WriteEntry("FILE ACTION - Copy " + _srcPath + relativePath + " to " + _tgtPath + relativePath);
							long fileSize = new FileInfo(_srcPath + relativePath).Length;
							Logger.WriteLog(Logger.LogType.CopySRC, _srcPath + relativePath, fileSize, _tgtPath + relativePath, fileSize);
						}
						else
						{
							String delRelativePath = _srcRenameList.PriSub[relativePath];
							FileUnit fileMeta = _srcDirtyFilesList.getByPrimary(relativePath);
							_updatedList.add(relativePath, fileMeta.Hash.Substring(2), fileMeta);
							checkAndCreateFolder(_tgtPath + relativePath);
							File.Move(_tgtPath + delRelativePath, _tgtPath + relativePath);
							_tgtCleanFilesList.removeByPrimary(delRelativePath);
							_summary.iTgtFileRename++;
							//Logger.WriteEntry("FILE ACTION - Move " + _tgtPath + delRelativePath + " to " + _tgtPath + relativePath);
							long fileSize = new FileInfo(_tgtPath + relativePath).Length;
							Logger.WriteLog(Logger.LogType.RenameTGT, _tgtPath + delRelativePath, fileSize, _tgtPath + relativePath, fileSize);
						}
					}
					break;
				case mFlag:
					{
						FileUnit fileMeta = new FileUnit(_srcPath + relativePath);
						String hashCode = Utility.computeMyHash(fileMeta);
						fileMeta.Hash = hashCode;
						checkAndCreateFolder(_tgtPath + relativePath);
						File.Copy(_srcPath + relativePath, _tgtPath + relativePath, true);
						_updatedList.add(relativePath, hashCode, fileMeta);
						_tgtCleanFilesList.removeByPrimary(relativePath);
						_summary.iSrcFileCopy++; _summary.iTgtFileOverwrite++;
						//Logger.WriteEntry("FILE ACTION - Copy " + _srcPath + relativePath + " to " + _tgtPath + relativePath);
						long fileSize = new FileInfo(_srcPath + relativePath).Length;
						Logger.WriteLog(Logger.LogType.CopySRC, _srcPath + relativePath, fileSize, _tgtPath + relativePath, fileSize);
					}
					break;
				case dFlag:
					{
						long fileSize = new FileInfo(_tgtPath + relativePath).Length;
						File.Delete(_tgtPath + relativePath);
						_tgtCleanFilesList.removeByPrimary(relativePath);
						_summary.iTgtFileDelete++;
						//Logger.WriteEntry("FILE ACTION - Delete " + _tgtPath + relativePath);
						Logger.WriteLog(Logger.LogType.DeleteTGT, null, 0, _tgtPath + relativePath, fileSize);
					}
					break;
			}
		}

		/// <summary>
		/// Perform sync between source clean file and target dirty file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="relativePath"></param>
		/// <param name="srcFlag"></param>
		private void SyncSrcCleanTgtDirty(String relativePath, String tgtFlag)
		{
			switch (tgtFlag)
			{
				case cFlag:
					{
						if (!_tgtRenameList.containsPriKey(relativePath))
						{
							FileUnit fileMeta = new FileUnit(_tgtPath + relativePath);
							String hashCode = Utility.computeMyHash(fileMeta);
							fileMeta.Hash = hashCode;
							checkAndCreateFolder(_srcPath + relativePath);
							File.Copy(_tgtPath + relativePath, _srcPath + relativePath);
							_updatedList.add(relativePath, hashCode, fileMeta);
							_summary.iTgtFileCopy++;
							//Logger.WriteEntry("FILE ACTION - Copy " + _tgtPath + relativePath + " to " + _srcPath + relativePath);
							long fileSize = new FileInfo(_tgtPath + relativePath).Length;
							Logger.WriteLog(Logger.LogType.CopyTGT, _tgtPath + relativePath, fileSize, _tgtPath + relativePath, fileSize);
						}
						else
						{
							String delRelativePath = _tgtRenameList.PriSub[relativePath];
							if (_srcCleanFilesList.containsPriKey(delRelativePath))
							{
								FileUnit fileMeta = _tgtDirtyFilesList.getByPrimary(relativePath);
								_updatedList.add(relativePath, fileMeta.Hash.Substring(2), fileMeta);
								checkAndCreateFolder(_srcPath + relativePath);
								File.Move(_srcPath + delRelativePath, _srcPath + relativePath);
								_summary.iTgtFileRename++;
								//Logger.WriteEntry("FILE ACTION - Move " + _srcPath + delRelativePath + " to " + _srcPath + relativePath);
								long fileSize = new FileInfo(_srcPath + relativePath).Length;
								Logger.WriteLog(Logger.LogType.RenameSRC, _srcPath + delRelativePath, fileSize, _srcPath + relativePath, fileSize);
								_srcCleanFilesList.removeByPrimary(delRelativePath);
							}

						}
					}
					break;
				case mFlag:
					{
						FileUnit fileMeta = new FileUnit(_tgtPath + relativePath);
						String hashCode = Utility.computeMyHash(fileMeta);
						fileMeta.Hash = hashCode;
						checkAndCreateFolder(_srcPath + relativePath);
						File.Copy(_tgtPath + relativePath, _srcPath + relativePath, true);
						_updatedList.add(relativePath, hashCode, fileMeta);
						_srcCleanFilesList.removeByPrimary(relativePath);
						_summary.iTgtFileCopy++; _summary.iSrcFileOverwrite++;
						//Logger.WriteEntry("FILE ACTION - Copy " + _tgtPath + relativePath + " to " + _srcPath + relativePath);
						long fileSize = new FileInfo(_tgtPath + relativePath).Length;
						Logger.WriteLog(Logger.LogType.CopyTGT, _tgtPath + relativePath, fileSize, _srcPath + relativePath, fileSize);
					}
					break;
				case dFlag:
					{
						long fileSize = new FileInfo(_srcPath + relativePath).Length;
						File.Delete(_srcPath + relativePath);
						_srcCleanFilesList.removeByPrimary(relativePath);
						_summary.iSrcFileDelete++;
						//Logger.WriteEntry("FILE ACTION - Delete " + _srcPath + relativePath);
						Logger.WriteLog(Logger.LogType.DeleteSRC, _srcPath + relativePath, fileSize, null, 0);
					}
					break;
			}
		}

		/// <summary>
		/// Perform sync between source dirty file and target dirty file. dirty = modification, deletion and rename.
		/// </summary>
		/// <param name="srcRelativePath"></param>
		/// <param name="srcFlag"></param>
		/// <param name="srcFile"></param>
		/// <param name="tgtRelativePath"></param>
		/// <param name="tgtFlag"></param>
		/// <param name="tgtFile"></param>
		private void SyncSrcDirtyTgtDirty(String srcRelativePath, String srcFlag, FileUnit srcFile, String tgtRelativePath, String tgtFlag, FileUnit tgtFile)
		{
			if (_tgtDirtyFilesList.containsPriKey(tgtRelativePath))
			{
				if (_tgtRenameList.containsSecKey(tgtRelativePath) && !_tgtRenameList.containsPriKey(srcRelativePath))
				{
					tgtFlag = cFlag;
					List<String> lstFiles = _tgtRenameList.SubPri[tgtRelativePath];
					tgtRelativePath = lstFiles[0];
					tgtFile = _tgtRenameList.getByPrimary(tgtRelativePath);
				}
				else if (!_tgtDirtyFilesList.containsPriKey(srcRelativePath))
				{
					tgtFlag = "" + _tgtDirtyFilesList.PriSub[tgtRelativePath][0];
					tgtFile = _tgtDirtyFilesList.getByPrimary(tgtRelativePath);
				}
			}

			int iSrcSlash = srcRelativePath.LastIndexOf('\\');
			int iTgtSlash = tgtRelativePath.LastIndexOf('\\');

			String srcFilePath = "";
			if (iSrcSlash > 0) { srcFilePath = srcRelativePath.Substring(0, iSrcSlash+1); }

			String tgtFilePath = "";
			if (iTgtSlash > 0) { tgtFilePath = tgtRelativePath.Substring(0, iTgtSlash+1); }

			if (!srcFilePath.Equals(tgtFilePath))
			{
				FileUnit folderMeta;
				if (_taskSettings.FolderConflict == TaskSettings.ConflictFolderAction.KeepSourceName)
				{
					if (!tgtFlag.Equals(dFlag))
					{
						if (srcFlag.Equals(dFlag) && _srcDirtyFoldersList.containsSecKey(dFlag + "-" + srcFilePath))
						{
							//File.Move(_tgtPath + tgtRelativePath, _tgtPath + tgtFilePath + tgtRelativePath.Substring(iTgtSlash));
							srcFilePath = tgtFilePath;
						}
						else
						{
							checkAndCreateFolder(_tgtPath + srcRelativePath);
							File.Move(_tgtPath + tgtRelativePath, _tgtPath + srcFilePath + tgtRelativePath.Substring(iTgtSlash));
							folderMeta = new FileUnit(_tgtPath + srcFilePath);
							DirectoryInfo dir = new DirectoryInfo(_tgtPath + tgtFilePath);
							int iFileCount = dir.GetFiles().Length;
							int iDirCount = dir.GetDirectories().Length;

							if (iFileCount == 0 && iDirCount == 0)
							{
								if (_srcDirtyFoldersList.containsPriKey(tgtFilePath))
									_srcDirtyFoldersList.removeByPrimary(tgtFilePath);
								if (_tgtDirtyFoldersList.containsPriKey(tgtFilePath))
									_tgtDirtyFoldersList.removeByPrimary(tgtFilePath);
								_srcDirtyFoldersList.add(tgtFilePath, dFlag + "-" + tgtFilePath, folderMeta);
								_tgtDirtyFoldersList.add(tgtFilePath, dFlag + "-" + tgtFilePath, folderMeta);
							}
						}
					}
					tgtFilePath = srcFilePath;
				}
				else
				{

					if (!srcFlag.Equals(dFlag))
					{
						if (tgtFlag.Equals(dFlag) && _tgtDirtyFoldersList.containsSecKey(dFlag + "-" + tgtFilePath))
						{
							//File.Move(_srcPath + srcRelativePath, _srcPath + srcFilePath + srcRelativePath.Substring(iSrcSlash));
							tgtFilePath = srcFilePath;
						}
						else
						{
							checkAndCreateFolder(_srcPath + tgtRelativePath);
							File.Move(_srcPath + srcRelativePath, _srcPath + tgtFilePath + srcRelativePath.Substring(iSrcSlash));
							folderMeta = new FileUnit(_srcPath + tgtFilePath);
							DirectoryInfo dir = new DirectoryInfo(_srcPath + srcFilePath);
							int iFileCount = dir.GetFiles().Length;
							int iDirCount = dir.GetDirectories().Length;

							if (iFileCount == 0 && iDirCount == 0)
							{
								if (_srcDirtyFoldersList.containsPriKey(srcFilePath))
									_srcDirtyFoldersList.removeByPrimary(srcFilePath);
								if (_tgtDirtyFoldersList.containsPriKey(srcFilePath))
									_tgtDirtyFoldersList.removeByPrimary(srcFilePath);
								_srcDirtyFoldersList.add(srcFilePath, dFlag + "-" + srcFilePath, folderMeta);
								_tgtDirtyFoldersList.add(srcFilePath, dFlag + "-" + srcFilePath, folderMeta);
							}
						}
					}
					srcFilePath = tgtFilePath;
				}
				executeSyncAction(srcFile, tgtFile, srcFlag, tgtFlag, _srcPath + srcFilePath, _tgtPath + tgtFilePath);
				_tgtDirtyFilesList.removeByPrimary(tgtRelativePath);
			}
			else
			{
				executeSyncAction(srcFile, tgtFile, srcFlag, tgtFlag, _srcPath + srcFilePath, _tgtPath + srcFilePath);
				_tgtDirtyFilesList.removeByPrimary(tgtRelativePath);

				if (srcFlag.Equals(cFlag) && _srcRenameList.containsPriKey(srcRelativePath))
				{
					String delSrcFile = _srcRenameList.PriSub[srcRelativePath];

					if (_tgtCleanFilesList.containsPriKey(delSrcFile)) _tgtCleanFilesList.removeByPrimary(delSrcFile);
					if (File.Exists(_tgtPath + delSrcFile)) File.Delete(_tgtPath + delSrcFile);

					if (tgtFlag.Equals(cFlag) && _tgtRenameList.containsPriKey(srcRelativePath))
					{
						String delTgtFile = _tgtRenameList.PriSub[srcRelativePath];
						if (_srcCleanFilesList.containsPriKey(delTgtFile)) _srcCleanFilesList.removeByPrimary(delTgtFile);
						if (File.Exists(_srcPath + delTgtFile)) File.Delete(_srcPath + delTgtFile);
					}
				}
				else if (srcFlag.Equals(cFlag) && _tgtRenameList.containsPriKey(srcRelativePath))
				{
					String delTgtFile = _tgtRenameList.PriSub[srcRelativePath];

					if (_srcCleanFilesList.containsPriKey(delTgtFile)) _srcCleanFilesList.removeByPrimary(delTgtFile);
					if (File.Exists(_srcPath + delTgtFile)) File.Delete(_srcPath + delTgtFile);
				}
			}

		}

		/// <summary>
		/// Perform folder cleanup after synchronization.
		/// </summary>
		private void SyncFoldersCleanup()
		{
			foreach (var mySrcRecord in _srcDirtyFoldersList.PriSub)
			{
				String relativePath = mySrcRecord.Key;
				String srcSecondKey = mySrcRecord.Value;
				String srcFlag = "" + srcSecondKey[0];
				FileUnit srcFileMeta = _srcDirtyFoldersList.getByPrimary(relativePath);

				if (srcFlag.Equals(cFlag))
				{
					_summary.iSrcFolderCreate++;
					//Logger.WriteEntry("FOLDER ACTION - Create " + _srcPath + relativePath);
					Logger.WriteLog(Logger.LogType.CreateTGT, null, 0, _tgtPath + relativePath, 0);
					if (!Directory.Exists(_srcPath + relativePath))
					{ Directory.CreateDirectory(_srcPath + relativePath); }
					if (!Directory.Exists(_tgtPath + relativePath))
					{ Directory.CreateDirectory(_tgtPath + relativePath); }
					if (_tgtDirtyFoldersList.containsSecKey(cFlag + "-" + relativePath))
					{ _tgtDirtyFoldersList.removeByPrimary(relativePath); }
					if (!_updatedList.containsPriKey(relativePath))
					{ _updatedList.add(relativePath, relativePath, srcFileMeta); }
				}
				else if (srcFlag.Equals(dFlag))
				{
					if (_tgtDirtyFoldersList.containsSecKey(dFlag + "-" + relativePath))
					{
						if (Directory.Exists(_srcPath + relativePath))
						{
							Directory.Delete(_srcPath + relativePath, true);
							_summary.iSrcFolderDelete++;
							//Logger.WriteEntry("FOLDER ACTION - Delete " + _srcPath + relativePath);
							Logger.WriteLog(Logger.LogType.DeleteSRC, _srcPath + relativePath, 0, null, 0);
						}
						if (Directory.Exists(_tgtPath + relativePath))
						{
							Directory.Delete(_tgtPath + relativePath, true);
							_summary.iTgtFolderDelete++;
							//Logger.WriteEntry("FOLDER ACTION - Delete " + _tgtPath + relativePath);
							Logger.WriteLog(Logger.LogType.DeleteTGT, null, 0, _tgtPath + relativePath, 0);
						}
						_tgtDirtyFoldersList.removeByPrimary(relativePath);
					}
					else if (_tgtCleanFoldersList.containsPriKey(relativePath))
					{
						if (Directory.Exists(_srcPath + relativePath))
						{
							_tgtCleanFoldersList.removeByPrimary(relativePath);
							_updatedList.add(relativePath, relativePath, srcFileMeta);
						}
						else
						{
							if (_tgtCleanFoldersList.containsPriKey(relativePath))
								_tgtCleanFoldersList.removeByPrimary(relativePath);
							if (Directory.Exists(_tgtPath + relativePath))
							{
								Directory.Delete(_tgtPath + relativePath, true);
								_summary.iTgtFolderDelete++;
								//Logger.WriteEntry("FOLDER ACTION - Delete " + _tgtPath + relativePath);
								Logger.WriteLog(Logger.LogType.DeleteTGT, null, 0, _tgtPath + relativePath, 0);
							}
						}
					}
				}
			}
			foreach (var myTgtRecord in _tgtDirtyFoldersList.PriSub)
			{
				String relativePath = myTgtRecord.Key;
				String tgtSecondKey = myTgtRecord.Value;
				String tgtFlag = "" + tgtSecondKey[0];
				FileUnit tgtFileMeta = _tgtDirtyFoldersList.getByPrimary(relativePath);
				if (tgtFlag.Equals(cFlag))
				{
					_summary.iTgtFolderCreate++;
					//Logger.WriteEntry("FOLDER ACTION - Create " + _tgtPath + relativePath);
					Logger.WriteLog(Logger.LogType.CreateSRC, _srcPath + relativePath, 0, null, 0);
					if (!Directory.Exists(_srcPath + relativePath))
					{ Directory.CreateDirectory(_srcPath + relativePath); }
					if (!Directory.Exists(_tgtPath + relativePath))
					{ Directory.CreateDirectory(_tgtPath + relativePath); }
					if (!_updatedList.containsPriKey(relativePath))
					{ _updatedList.add(relativePath, relativePath, tgtFileMeta); }
				}
				else if (tgtFlag.Equals(dFlag))
				{
					if (_srcCleanFoldersList.containsPriKey(relativePath))
					{
						if (Directory.Exists(_tgtPath + relativePath))
						{
							_srcCleanFoldersList.removeByPrimary(relativePath);
							_updatedList.add(relativePath, relativePath, tgtFileMeta);
						}
						else
						{
							if (_srcCleanFoldersList.containsPriKey(relativePath))
								_srcCleanFoldersList.removeByPrimary(relativePath);
							if (Directory.Exists(_srcPath + relativePath))
							{
								Directory.Delete(_srcPath + relativePath, true);
								_summary.iSrcFolderDelete++;
								//Logger.WriteEntry("FOLDER ACTION - Delete " + _srcPath + relativePath);
								Logger.WriteLog(Logger.LogType.DeleteSRC, _srcPath + relativePath, 0, null, 0);
							}
						}
					}
				}
			}
			foreach (var myRecord in _srcCleanFoldersList.PriSub)
			{
				String relativePath = myRecord.Key;
				//FileUnit fileMeta = _srcCleanFilesList.Primary[relativePath];
				FileUnit fileMeta = _srcCleanFoldersList.Primary[relativePath];
				_updatedList.add(relativePath, relativePath, fileMeta);
			}
		}

		/// <summary>
		/// Select the desired action for conflicting files.
		/// </summary>
		/// <param name="sourceDirtyFile"></param>
		/// <param name="destDirtyFile"></param>
		/// <param name="s"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		private SyncAction checkConflicts(FileUnit sourceDirtyFile, FileUnit destDirtyFile, String s, String t)
		{
			//Compare every single file in dirty source list with dirty destination list to determine what action 
			//to be taken. There are total of four kinds of flags associate with every file in dirty source list. 
			if (s.Equals(mFlag) || s.Equals(rFlag))
			{
				if (t.Equals(mFlag) || t.Equals(cFlag) || t.Equals(rFlag))
				{
					switch (_taskSettings.SrcTgtConflict)
					{
						case TaskSettings.ConflictSrcTgtAction.KeepBothCopies:
							return SyncAction.KeepBothCopies;
						case TaskSettings.ConflictSrcTgtAction.KeepLatestCopy:
							if (sourceDirtyFile.LastWriteTime > destDirtyFile.LastWriteTime)	//keep the lastest file if two files are both modified
							{ return SyncAction.CopyFileToTarget; }
							else
							{ return SyncAction.CopyFileToSource; }
						case TaskSettings.ConflictSrcTgtAction.SourceOverwriteTarget:
							return SyncAction.CopyFileToTarget;
						case TaskSettings.ConflictSrcTgtAction.TargetOverwriteSource:
							return SyncAction.CopyFileToSource;
						default:
							return SyncAction.KeepBothCopies;
					}
				}
				else if (t.Equals(dFlag)) //There are two possibilities for a file which was marked as DELETE on destination dirty list.
				{
					switch (_taskSettings.SrcConflict)
					{
						case TaskSettings.ConflictSrcAction.CopyFileToTarget:
							return SyncAction.CopyFileToTarget;//copy source to destination
						case TaskSettings.ConflictSrcAction.DeleteSourceFile:
							return SyncAction.DeleteSourceFile;//delete source file
						default:
							return SyncAction.CopyFileToTarget;
					}
				}
				else
				{ return SyncAction.NoAction; }
			}
			else if (s.Equals(cFlag))
			{
				if (t.Equals(cFlag))
				{
					// Check Renaming. We suspect the two files has been renamed(i.e.contents are the same) if both of them 
					// were marked as CREATE, so we need to check their contents.
					switch (_taskSettings.SrcTgtConflict)
					{
						case TaskSettings.ConflictSrcTgtAction.KeepBothCopies:
							if (sourceDirtyFile.Name.Equals(destDirtyFile.Name) &&
									sourceDirtyFile.LastWriteTime.Equals(destDirtyFile.LastWriteTime) &&
									sourceDirtyFile.Hash.Equals(destDirtyFile.Hash))
								return SyncAction.NoAction;
							else
								return SyncAction.KeepBothCopies;
						case TaskSettings.ConflictSrcTgtAction.KeepLatestCopy:
							if (sourceDirtyFile.LastWriteTime > destDirtyFile.LastWriteTime)	//keep the lastest file if two files are both modified
							{ return SyncAction.CopyFileToTarget; }
							else
							{ return SyncAction.CopyFileToSource; }
						case TaskSettings.ConflictSrcTgtAction.SourceOverwriteTarget:
							return SyncAction.CopyFileToTarget;
						case TaskSettings.ConflictSrcTgtAction.TargetOverwriteSource:
							return SyncAction.CopyFileToSource;
						default:
							return SyncAction.KeepBothCopies;
					}
				}
				else if (t.Equals(dFlag))
				{
					switch (_taskSettings.SrcConflict)
					{
						case TaskSettings.ConflictSrcAction.CopyFileToTarget:
							return SyncAction.CopyFileToTarget;//copy source to destination
						case TaskSettings.ConflictSrcAction.DeleteSourceFile:
							return SyncAction.DeleteSourceFile;//delete source file
						default:
							return SyncAction.CopyFileToTarget;
					}
				}
				else if (t.Equals(rFlag) || t.Equals(mFlag))
				{
					switch (_taskSettings.SrcTgtConflict)
					{
						case TaskSettings.ConflictSrcTgtAction.KeepBothCopies:
							return SyncAction.KeepBothCopies;
						case TaskSettings.ConflictSrcTgtAction.KeepLatestCopy:
							if (sourceDirtyFile.LastWriteTime > destDirtyFile.LastWriteTime)	//keep the lastest file if two files are both modified
							{ return SyncAction.CopyFileToTarget; }
							else
							{ return SyncAction.CopyFileToSource; }
						case TaskSettings.ConflictSrcTgtAction.SourceOverwriteTarget:
							return SyncAction.CopyFileToTarget;
						case TaskSettings.ConflictSrcTgtAction.TargetOverwriteSource:
							return SyncAction.CopyFileToSource;
						default:
							return SyncAction.KeepBothCopies;
					}
				}
				else
				{ return SyncAction.NoAction; }
			}
			else if (s.Equals(dFlag))
			{
				if (t.Equals(cFlag) || t.Equals(mFlag))
				{
					switch (_taskSettings.TgtConflict)
					{
						case TaskSettings.ConflictTgtAction.CopyFileToSource:
							return SyncAction.CopyFileToSource;//copy destination to source
						case TaskSettings.ConflictTgtAction.DeleteTargetFile:
							return SyncAction.DeleteTargetFile;//delete destination file
						default:
							return SyncAction.CopyFileToSource;
					}
				}
				else if (t.Equals(dFlag))
				{ return SyncAction.NoAction; }
				else
				{ return SyncAction.NoAction; }
			}
			else
				return SyncAction.NoAction;
		}

		/// <summary>
		/// Perform file operation for conflicting files.
		/// </summary>
		/// <param name="srcFile"></param>
		/// <param name="tgtFile"></param>
		/// <param name="srcFlag"></param>
		/// <param name="tgtFlag"></param>
		/// <param name="srcPath"></param>
		/// <param name="tgtPath"></param>
		private void executeSyncAction(FileUnit srcFile, FileUnit tgtFile, String srcFlag, String tgtFlag, String srcPath, String tgtPath)
		{
			switch (checkConflicts(srcFile, tgtFile, srcFlag, tgtFlag))
			{
				case SyncAction.KeepBothCopies:
					{
						String srcFileName = srcFile.Name;
						String tgtFileName = tgtFile.Name;
						if (srcFile.Name.Equals(tgtFile.Name))
						{
							do
							{
								int iExt = srcFileName.LastIndexOf('.');		// Locate the index position of the file extension
								if (iExt > 0) 	// File with file extension
								{
									srcFileName = srcFileName.Substring(0, iExt) + "(1)" + srcFileName.Substring(iExt);
									tgtFileName = tgtFileName.Substring(0, iExt) + "(2)" + tgtFileName.Substring(iExt);
								}
								else if (iExt == -1)	// File without file extension
								{
									srcFileName = srcFileName + "(1)";
									tgtFileName = tgtFileName + "(2)";
								}
							} while (File.Exists(srcPath + srcFileName) || File.Exists(srcPath + tgtFileName)
											|| File.Exists(tgtPath + srcFileName) || File.Exists(tgtPath + tgtFileName));
							File.Move(srcPath + srcFile.Name, srcPath + srcFileName);
							File.Move(tgtPath + tgtFile.Name, tgtPath + tgtFileName);
						}
						checkAndCreateFolder(tgtPath + srcFileName);
						checkAndCreateFolder(srcPath + tgtFileName);
						File.Copy(srcPath + srcFileName, tgtPath + srcFileName);
						File.Copy(tgtPath + tgtFileName, srcPath + tgtFileName);
						FileUnit srcFileMeta = new FileUnit(srcPath + srcFileName);
						String srcHashcode = Utility.computeMyHash(srcFileMeta);
						srcFileMeta.Hash = srcHashcode;
						FileUnit tgtFileMeta = new FileUnit(tgtPath + tgtFileName);
						String tgtHashcode = Utility.computeMyHash(tgtFileMeta);
						tgtFileMeta.Hash = tgtHashcode;
						_updatedList.add(srcPath.Substring(_srcPath.Length) + srcFileName, srcHashcode, srcFileMeta);
						_updatedList.add(tgtPath.Substring(_tgtPath.Length) + tgtFileName, tgtHashcode, tgtFileMeta);
						_summary.iSrcFileCopy++; _summary.iTgtFileCopy++;
						long fileSize = new FileInfo(srcPath + srcFileName).Length;
						Logger.WriteLog(Logger.LogType.CopySRC, srcPath + srcFileName, fileSize, tgtPath + srcFileName, fileSize);
						fileSize = new FileInfo(tgtPath + tgtFileName).Length;
						Logger.WriteLog(Logger.LogType.CopyTGT, tgtPath + tgtFileName, fileSize, srcPath + tgtFileName, fileSize);
					}
					break;
				case SyncAction.CopyFileToTarget:
					{
						FileUnit fileMeta = new FileUnit(srcPath + srcFile.Name);
						String strHashcode = Utility.computeMyHash(fileMeta);
						fileMeta.Hash = strHashcode;
						checkAndCreateFolder(tgtPath + srcFile.Name);
						File.Copy(srcPath + srcFile.Name, tgtPath + srcFile.Name, true);
						_updatedList.add(srcPath.Substring(_srcPath.Length) + srcFile.Name, strHashcode, fileMeta);
						_summary.iSrcFileCopy++;
						long fileSize = new FileInfo(srcPath + srcFile.Name).Length;
						Logger.WriteLog(Logger.LogType.CopySRC, srcPath + srcFile.Name, fileSize, tgtPath + srcFile.Name, fileSize);
					}
					break;
				case SyncAction.CopyFileToSource:
					{
						FileUnit fileMeta = new FileUnit(tgtPath + tgtFile.Name);
						String strHashcode = Utility.computeMyHash(fileMeta);
						fileMeta.Hash = strHashcode;
						checkAndCreateFolder(srcPath + tgtFile.Name);
						File.Copy(tgtPath + tgtFile.Name, srcPath + tgtFile.Name, true);
						_updatedList.add(tgtPath.Substring(_tgtPath.Length) + tgtFile.Name, strHashcode, fileMeta);
						_summary.iTgtFileCopy++;
						long fileSize = new FileInfo(tgtPath + tgtFile.Name).Length;
						Logger.WriteLog(Logger.LogType.CopyTGT, tgtPath + tgtFile.Name, fileSize, srcPath + tgtFile.Name, fileSize);
					}
					break;
				case SyncAction.DeleteSourceFile:
					{
						long fileSize = new FileInfo(srcPath + srcFile.Name).Length;
						File.Delete(srcPath + srcFile.Name);
						_summary.iSrcFileDelete++;
						Logger.WriteLog(Logger.LogType.DeleteSRC, srcPath + srcFile.Name, fileSize, null, 0);
						
					}
					break;
				case SyncAction.DeleteTargetFile:
					{
						long fileSize = new FileInfo(tgtPath + tgtFile.Name).Length;
						File.Delete(tgtPath + tgtFile.Name);
						_summary.iTgtFileDelete++;
						Logger.WriteLog(Logger.LogType.DeleteTGT, null, 0, tgtPath + tgtFile.Name, fileSize);
					}
					break;
				case SyncAction.NoAction:
					{
						if (srcFlag.Equals(cFlag) && tgtFlag.Equals(cFlag))
						{
							String relPath = (srcPath + srcFile.Name).Substring(_srcPath.Length);
							if (!_updatedList.containsPriKey(relPath))
							{
								FileUnit fileMeta = new FileUnit(_srcPath + relPath);
								String hashCode = Utility.computeMyHash(fileMeta);
								fileMeta.Hash = hashCode;
								_updatedList.add(relPath, hashCode, fileMeta);
							}
						}
					}
					break;
			}
		}

		/// <summary>
		/// Check and Create before folders perform file operation.
		/// </summary>
		/// <param name="fullPath"></param>
		private void checkAndCreateFolder(String fullPath)
		{
			String[] strFolders = fullPath.Split('\\');
			String strFolder = strFolders[0];
			for (int i = 1; i < strFolders.Length - 1; i++)
			{
				strFolder = strFolder + "\\" + strFolders[i];
				if (!Directory.Exists(strFolder))
					Directory.CreateDirectory(strFolder);
			}
		}
	}
}