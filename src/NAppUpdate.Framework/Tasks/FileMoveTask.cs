using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Sources;
using NAppUpdate.Framework.Utils;
using System;
using System.IO;

namespace NAppUpdate.Framework.Tasks
{
	[Serializable]
	[UpdateTaskAlias("fileMove")]
	class FileMoveTask : UpdateTaskBase
	{
		[NauField("fromPath", "The local path of the file to move", true)]
		public string FromPath { get; set; }

		[NauField("toPath", "The local path destination of the file to move", true)]
		public string ToPath { get; set; }

		[NauField("hotswap",
			"Default update action is a cold update; check here if a hot file swap should be attempted"
			, false)]
		public bool CanHotSwap { get; set; }

		private string _fromFile, _toFile, _backupFile;

		public override void Prepare(IUpdateSource source)
		{
			if (string.IsNullOrEmpty(FromPath) || string.IsNullOrEmpty(ToPath) || FromPath.Equals(ToPath))
			{
				UpdateManager.Instance.Logger.Log(Logger.SeverityLevel.Warning, "FileMoveTask: FromPath and/or ToPath are empty or the paths are the same, task is a noop");
				return; // Errorneous case, but there's nothing to prepare to, and by default we prefer a noop over an error
			}

			_fromFile = Path.Combine(Path.GetDirectoryName(UpdateManager.Instance.ApplicationPath), FromPath);
			_toFile = Path.Combine(Path.GetDirectoryName(UpdateManager.Instance.ApplicationPath), ToPath);
			UpdateManager.Instance.Logger.Log("FileMoveTask: Prepared successfully; {0} -> {1}", _fromFile, _toFile);
		}

		public override TaskExecutionStatus Execute(bool coldRun)
		{
			if (string.IsNullOrEmpty(FromPath) || string.IsNullOrEmpty(ToPath) || FromPath.Equals(ToPath) || !File.Exists(_fromFile))
			{
				UpdateManager.Instance.Logger.Log(Logger.SeverityLevel.Warning, "FileMoveTask: FromPath and/or ToPath are empty or the paths are the same, task is a noop");
				return TaskExecutionStatus.Successful; // Errorneous case, but there's nothing to prepare to, and by default we prefer a noop over an error
			}

			var dirName = Path.GetDirectoryName(_toFile);
			if (!Directory.Exists(dirName))
				FileSystem.CreateDirectoryStructure(dirName, false);

			// Create a backup copy if target exists
			if (_backupFile == null && File.Exists(_toFile))
			{
				_backupFile = Path.Combine(UpdateManager.Instance.Config.BackupFolder, ToPath);
				if (!Directory.Exists(Path.GetDirectoryName(_backupFile)))
					FileSystem.CreateDirectoryStructure(Path.GetDirectoryName(_backupFile), false);
				File.Copy(_toFile, _backupFile, true);
			}

			// Only allow execution if the apply attribute was set to hot-swap, or if this is a cold run
			if (CanHotSwap || coldRun)
			{
				try
				{
					if (File.Exists(_toFile))
						File.Delete(_toFile);

					File.Move(_fromFile, _toFile);
				}
				catch (Exception)
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
			if (File.Exists(_fromFile) && !PermissionsCheck.HaveWritePermissionsForFileOrFolder(_fromFile) ||
				File.Exists(_toFile) && !PermissionsCheck.HaveWritePermissionsForFileOrFolder(_toFile))
			{
				return TaskExecutionStatus.RequiresPrivilegedAppRestart;
			}
			return TaskExecutionStatus.RequiresAppRestart;
		}

		public override bool Rollback()
		{
			if (string.IsNullOrEmpty(FromPath) || string.IsNullOrEmpty(ToPath) || FromPath.Equals(ToPath))
				return true;

			// Copy the files back.
			if (File.Exists(_toFile) && !File.Exists(_fromFile))
				File.Move(_toFile, _fromFile);
			if (File.Exists(_backupFile) && !File.Exists(_toFile))
				File.Move(_backupFile, _toFile);
			return true;
		}
	}
}
