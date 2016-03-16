using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Sources;
using NAppUpdate.Framework.Utils;
using System;
using System.IO;

namespace NAppUpdate.Framework.Tasks
{
	[Serializable]
	[UpdateTaskAlias("fileDelete")]
	class FileDeleteTask : UpdateTaskBase
	{
		[NauField("localPath", "The local path of the file to delete", true)]
		public string LocalPath { get; set; }

		[NauField("hotswap",
			"Default update action is a cold update; check here if a hot file swap should be attempted"
			, false)]
		public bool CanHotSwap { get; set; }

		private string _destinationFile, _backupFile;

		public override void Prepare(IUpdateSource source)
		{
			if (string.IsNullOrEmpty(LocalPath))
			{
				UpdateManager.Instance.Logger.Log(Logger.SeverityLevel.Warning, "FileDeleteTask: LocalPath is empty, task is a noop");
				return; // Errorneous case, but there's nothing to prepare to, and by default we prefer a noop over an error
			}

			_destinationFile = Path.Combine(Path.GetDirectoryName(UpdateManager.Instance.ApplicationPath), LocalPath);
			UpdateManager.Instance.Logger.Log("FileDeleteTask: Prepared successfully; destination file: {0}", _destinationFile);
		}

		public override TaskExecutionStatus Execute(bool coldRun)
		{
			if (string.IsNullOrEmpty(LocalPath))
			{
				UpdateManager.Instance.Logger.Log(Logger.SeverityLevel.Warning, "FileDeleteTask: LocalPath is empty, task is a noop");
				return TaskExecutionStatus.Successful; // Errorneous case, but there's nothing to prepare to, and by default we prefer a noop over an error
			}

			var dirName = Path.GetDirectoryName(_destinationFile);
			if (!Directory.Exists(dirName))
			{
				UpdateManager.Instance.Logger.Log(Logger.SeverityLevel.Warning, "FileDeleteTask: Directory of file doesn't exist, task is a noop");
				return TaskExecutionStatus.Successful;
			}

			// Create a backup copy if target exists
			if (_backupFile == null && File.Exists(_destinationFile))
			{
				if (!Directory.Exists(Path.GetDirectoryName(Path.Combine(UpdateManager.Instance.Config.BackupFolder, LocalPath))))
					Utils.FileSystem.CreateDirectoryStructure(
						Path.GetDirectoryName(Path.Combine(UpdateManager.Instance.Config.BackupFolder, LocalPath)), false);
				_backupFile = Path.Combine(UpdateManager.Instance.Config.BackupFolder, LocalPath);
				File.Copy(_destinationFile, _backupFile, true);
			}

			// Only allow execution if the apply attribute was set to hot-swap, or if this is a cold run
			if (CanHotSwap || coldRun)
			{
				try
				{
					if (File.Exists(_destinationFile))
						File.Delete(_destinationFile);
				}
				catch (Exception ex)
				{
					if (!coldRun)
					{
						// Failed hot swap file tasks should now downgrade to cold tasks automatically
						CanHotSwap = false;
					}
				}
			}

			if (coldRun || CanHotSwap)
				// If we got thus far, we have completed execution
				return TaskExecutionStatus.Successful;

			// Otherwise, figure out what restart method to use
			if (File.Exists(_destinationFile) && !Utils.PermissionsCheck.HaveWritePermissionsForFileOrFolder(_destinationFile))
			{
				return TaskExecutionStatus.RequiresPrivilegedAppRestart;
			}
			return TaskExecutionStatus.RequiresAppRestart;
		}

		public override bool Rollback()
		{
			if (string.IsNullOrEmpty(_destinationFile))
				return true;

			// Copy the backup copy back to its original position
			if (File.Exists(_destinationFile))
				File.Delete(_destinationFile);
			File.Copy(_backupFile, _destinationFile, true);

			return true;
		}
	}
}
