Imports System.IO
Imports Microsoft.Extensions.Logging
Imports Microsoft.Extensions.DependencyInjection

Public Class FileLogger
    Implements ILogger

    Private ReadOnly _categoryName As String
    Private ReadOnly _filePath As String
    Private Shared ReadOnly _lock As New Object()

    Public Sub New(categoryName As String)
        _categoryName = categoryName
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "printers.log")
    End Sub

    Public Function BeginScope(Of TState)(state As TState) As IDisposable Implements ILogger.BeginScope
        Return Nothing
    End Function

    Public Function IsEnabled(logLevel As LogLevel) As Boolean Implements ILogger.IsEnabled
        Return True
    End Function

    Public Sub Log(Of TState)(logLevel As LogLevel, eventId As EventId, state As TState, exception As Exception, formatter As Func(Of TState, Exception, String)) Implements ILogger.Log
        SyncLock _lock
            Try
                Dim message = formatter(state, exception)
                Dim logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {_categoryName}: {message}"
                File.AppendAllText(_filePath, logLine & Environment.NewLine)
                If exception IsNot Nothing Then
                    File.AppendAllText(_filePath, exception.ToString() & Environment.NewLine)
                End If
            Catch
            End Try
        End SyncLock
    End Sub
End Class

Public Class FileLoggerProvider
    Implements ILoggerProvider

    Public Function CreateLogger(categoryName As String) As ILogger Implements ILoggerProvider.CreateLogger
        Return New FileLogger(categoryName)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Public Module FileLoggerExtensions
    <System.Runtime.CompilerServices.Extension>
    Public Sub AddFile(loggingBuilder As ILoggingBuilder)
        loggingBuilder.Services.AddSingleton(Of ILoggerProvider)(New FileLoggerProvider())
    End Sub
End Module
