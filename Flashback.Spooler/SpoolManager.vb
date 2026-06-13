Imports System
Imports System.IO
Imports System.Threading
Imports Flashback.Spooler.Models
Imports Microsoft.Extensions.Logging
Public Class SpoolManager
    Private ReadOnly _logger As ILogger
    Private ReadOnly _config As StorageConfig
    Private ReadOnly _spoolDirectory As String
    Private _sequenceNumber As Integer = 0

    Public Sub New(logger As ILogger, config As StorageConfig)
        _logger = logger
        _config = config
        
        ' Normalize directory path
        _spoolDirectory = config.SpoolDirectory.Replace("\"c, Path.DirectorySeparatorChar).Replace("/"c, Path.DirectorySeparatorChar)
        
        ' Ensure spool directory exists
        InitializeSpoolDirectory()
    End Sub
    Private Sub InitializeSpoolDirectory()
        Try
            If Not Directory.Exists(_spoolDirectory) Then
                Directory.CreateDirectory(_spoolDirectory)
                _logger.LogInformation("Created spool directory: {SpoolDir}", _spoolDirectory)
            Else
                _logger.LogInformation("Using existing spool directory: {SpoolDir}", _spoolDirectory)
            End If
        Catch ex As Exception
            _logger.LogError(ex, "Failed to initialize spool directory: {SpoolDir}", _spoolDirectory)
            Throw
        End Try
    End Sub
    Public Function CreateSpoolFile() As String
        Dim sequence = Interlocked.Increment(_sequenceNumber)
        Dim timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss")
        Dim filename = $"job_{timestamp}_{sequence:D6}.dat"
        Dim fullPath = Path.Combine(_spoolDirectory, filename)
        
        Try
            ' Create empty file to reserve the name
            Using fs = File.Create(fullPath)
                ' File created
            End Using
            
            _logger.LogDebug("Created spool file: {SpoolFile}", filename)
            Return fullPath
        Catch ex As Exception
            _logger.LogError(ex, "Failed to create spool file: {SpoolFile}", filename)
            Throw
        End Try
    End Function
    Public Async Function WriteToSpoolFileAsync(filePath As String, data As Byte(), cancellationToken As CancellationToken) As Task
        Try
            Using fs As New FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None, 8192, True)
                Await fs.WriteAsync(data, 0, data.Length, cancellationToken)
            End Using
        Catch ex As Exception
            _logger.LogError(ex, "Failed to write to spool file: {SpoolFile}", Path.GetFileName(filePath))
            Throw
        End Try
    End Function
    Public Async Function ReadFromSpoolFileAsync(filePath As String, buffer As Byte(), cancellationToken As CancellationToken) As Task(Of Integer)
        Try
            Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, True)
                Return Await fs.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
            End Using
        Catch ex As Exception
            _logger.LogError(ex, "Failed to read from spool file: {SpoolFile}", Path.GetFileName(filePath))
            Throw
        End Try
    End Function
    Public Function OpenSpoolFileForRead(filePath As String) As FileStream
        Try
            Return New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, True)
        Catch ex As Exception
            _logger.LogError(ex, "Failed to open spool file for reading: {SpoolFile}", Path.GetFileName(filePath))
            Throw
        End Try
    End Function
    Public Function GetSpoolFileSize(filePath As String) As Long
        Try
            Dim fileInfo As New FileInfo(filePath)
            Return fileInfo.Length
        Catch ex As Exception
            _logger.LogError(ex, "Failed to get spool file size: {SpoolFile}", Path.GetFileName(filePath))
            Return 0
        End Try
    End Function
    Public Sub DeleteSpoolFile(filePath As String)
        If String.IsNullOrEmpty(filePath) Then Return
        
        Try
            If File.Exists(filePath) Then
                File.Delete(filePath)
                _logger.LogDebug("Deleted spool file: {SpoolFile}", Path.GetFileName(filePath))
            End If
        Catch ex As Exception
            _logger.LogWarning(ex, "Failed to delete spool file: {SpoolFile}", Path.GetFileName(filePath))
            ' Don't throw - file deletion failure shouldn't stop the service
        End Try
    End Sub
    Public Sub CleanupOldSpoolFiles()
        Try
            Dim files = Directory.GetFiles(_spoolDirectory, "job_*.dat")
            
            If files.Length = 0 Then
                _logger.LogDebug("No spool files to clean up.")
                Return
            End If
            
            Dim deletedCount = 0
            Dim now = DateTime.Now
            
            ' Sort files by creation time (oldest first)
            Dim fileInfos = files.Select(Function(f) New FileInfo(f)).OrderBy(Function(fi) fi.CreationTime).ToArray()
            
            ' Delete files older than MaxSpoolAge
            For Each fileInfo In fileInfos
                Dim age = now - fileInfo.CreationTime
                If age.TotalHours > _config.MaxSpoolAge Then
                    Try
                        fileInfo.Delete()
                        deletedCount += 1
                        _logger.LogInformation("Deleted expired spool file: {SpoolFile} (age: {Age:F1} hours)", 
                                             fileInfo.Name, age.TotalHours)
                    Catch ex As Exception
                        _logger.LogWarning(ex, "Failed to delete expired spool file: {SpoolFile}", fileInfo.Name)
                    End Try
                End If
            Next
            
            ' If still over MaxSpoolFiles limit, delete oldest files
            Dim remainingFiles = Directory.GetFiles(_spoolDirectory, "job_*.dat")
            If remainingFiles.Length > _config.MaxSpoolFiles Then
                Dim filesToDelete = remainingFiles.Length - _config.MaxSpoolFiles
                Dim sortedRemaining = remainingFiles.Select(Function(f) New FileInfo(f)).OrderBy(Function(fi) fi.CreationTime).Take(filesToDelete)
                
                For Each fileInfo In sortedRemaining
                    Try
                        fileInfo.Delete()
                        deletedCount += 1
                        _logger.LogInformation("Deleted spool file due to count limit: {SpoolFile}", fileInfo.Name)
                    Catch ex As Exception
                        _logger.LogWarning(ex, "Failed to delete spool file: {SpoolFile}", fileInfo.Name)
                    End Try
                Next
            End If
            
            If deletedCount > 0 Then
                _logger.LogInformation("Cleanup complete. Deleted {Count} spool files.", deletedCount)
            Else
                _logger.LogDebug("Cleanup complete. No files needed deletion.")
            End If
            
        Catch ex As Exception
            _logger.LogError(ex, "Error during spool file cleanup")
        End Try
    End Sub
    Public Function GetSpoolStatistics() As (FileCount As Integer, TotalSize As Long)
        Try
            Dim files = Directory.GetFiles(_spoolDirectory, "job_*.dat")
            Dim totalSize As Long = 0
            
            For Each file In files
                Dim fileInfo As New FileInfo(file)
                totalSize += fileInfo.Length
            Next
            
            Return (files.Length, totalSize)
        Catch ex As Exception
            _logger.LogError(ex, "Failed to get spool statistics")
            Return (0, 0)
        End Try
    End Function
    Public Function HasSufficientSpace(estimatedSizeMB As Integer) As Boolean
        Try
            Dim driveInfo As New DriveInfo(Path.GetPathRoot(_spoolDirectory))
            Dim availableSpaceMB = driveInfo.AvailableFreeSpace / (1024 * 1024)
            
            ' Require at least 100MB free space plus the estimated job size
            Dim requiredSpaceMB = 100 + estimatedSizeMB
            
            If availableSpaceMB < requiredSpaceMB Then
                _logger.LogWarning("Insufficient disk space. Available: {Available}MB, Required: {Required}MB", 
                                 availableSpaceMB, requiredSpaceMB)
                Return False
            End If
            
            Return True
        Catch ex As Exception
            _logger.LogError(ex, "Failed to check disk space")
            Return True ' Assume sufficient space if check fails
        End Try
    End Function
End Class
