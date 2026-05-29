Imports System
Imports System.IO
Imports Flashback.Spooler.Models
Public Class ConfigManager
    Private ReadOnly _configPath As String
    Private _config As SpoolerConfig

    Public Sub New(Optional configPath As String = "spooler.conf")
        _configPath = configPath
        _config = New SpoolerConfig()
    End Sub
    Public Function LoadConfig() As SpoolerConfig
        If Not File.Exists(_configPath) Then
            Console.WriteLine($"Configuration file not found: {_configPath}")
            Console.WriteLine("Creating default configuration...")
            CreateDefaultConfig()
            Return _config
        End If

        Try
            Dim lines = File.ReadAllLines(_configPath)
            Dim currentSection As String = String.Empty

            For Each line In lines
                Dim trimmed = line.Trim()
                
                ' Skip empty lines and comments
                If String.IsNullOrWhiteSpace(trimmed) OrElse trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then
                    Continue For
                End If

                ' Section header
                If trimmed.StartsWith("[") AndAlso trimmed.EndsWith("]") Then
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).ToUpper()
                    Continue For
                End If

                ' Key=Value pair
                Dim parts = trimmed.Split("="c, 2)
                If parts.Length <> 2 Then Continue For

                Dim key = parts(0).Trim()
                Dim value = parts(1).Trim()

                ' Parse based on current section
                Select Case currentSection
                    Case "LISTENER"
                        ParseListenerConfig(key, value)
                    Case "STORAGE"
                        ParseStorageConfig(key, value)
                    Case "LOGGING"
                        ParseLoggingConfig(key, value)
                    Case "BEHAVIOR"
                        ParseBehaviorConfig(key, value)
                End Select
            Next

            Console.WriteLine("Configuration loaded successfully.")
            ValidateConfig()
            Return _config

        Catch ex As Exception
            Console.WriteLine($"Error loading configuration: {ex.Message}")
            Console.WriteLine("Using default configuration.")
            Return _config
        End Try
    End Function

    Private Sub ParseListenerConfig(key As String, value As String)
        Select Case key.ToUpper()
            Case "PORT9100ENABLED"
                _config.Listener.Port9100Enabled = ParseBool(value, True)
            Case "ENGINEPORT"
                _config.Listener.EnginePort = ParseInt(value, 9001)
        End Select
    End Sub

    Private Sub ParseStorageConfig(key As String, value As String)
        Select Case key.ToUpper()
            Case "SPOOLDIRECTORY"
                _config.Storage.SpoolDirectory = value
            Case "MAXSPOOLAGE"
                _config.Storage.MaxSpoolAge = ParseInt(value, 24)
            Case "MAXSPOOLFILES"
                _config.Storage.MaxSpoolFiles = ParseInt(value, 1000)
        End Select
    End Sub

    Private Sub ParseLoggingConfig(key As String, value As String)
        Select Case key.ToUpper()
            Case "LOGLEVEL"
                _config.Logging.LogLevel = value
            Case "LOGFILE"
                _config.Logging.LogFile = value
        End Select
    End Sub

    Private Sub ParseBehaviorConfig(key As String, value As String)
        Select Case key.ToUpper()
            Case "JOBCOMPLETIONTIMEOUT"
                _config.Behavior.JobCompletionTimeout = ParseInt(value, 5)
            Case "MAXJOBSIZEMB"
                _config.Behavior.MaxJobSizeMB = ParseInt(value, 100)
            Case "ENABLERETRY"
                _config.Behavior.EnableRetry = ParseBool(value, True)
            Case "MAXRETRIES"
                _config.Behavior.MaxRetries = ParseInt(value, 3)
            Case "RETRYDELAYSECONDS"
                _config.Behavior.RetryDelaySeconds = ParseInt(value, 30)
        End Select
    End Sub

    Private Function ParseBool(value As String, defaultValue As Boolean) As Boolean
        If String.IsNullOrWhiteSpace(value) Then Return defaultValue
        
        Select Case value.ToUpper()
            Case "TRUE", "YES", "1", "ON"
                Return True
            Case "FALSE", "NO", "0", "OFF"
                Return False
            Case Else
                Return defaultValue
        End Select
    End Function

    Private Function ParseInt(value As String, defaultValue As Integer) As Integer
        Dim result As Integer
        If Integer.TryParse(value, result) Then
            Return result
        End If
        Return defaultValue
    End Function

    Private Sub ValidateConfig()
        ' Validate port numbers
        If _config.Listener.EnginePort < 1 OrElse _config.Listener.EnginePort > 65535 Then
            Console.WriteLine($"Warning: Invalid EnginePort {_config.Listener.EnginePort}, using default 9001")
            _config.Listener.EnginePort = 9001
        End If

        ' Validate timeouts and limits
        If _config.Behavior.JobCompletionTimeout < 1 Then
            Console.WriteLine("Warning: JobCompletionTimeout must be at least 1 second, using default 5")
            _config.Behavior.JobCompletionTimeout = 5
        End If

        If _config.Behavior.MaxRetries < 0 Then
            Console.WriteLine("Warning: MaxRetries cannot be negative, using default 3")
            _config.Behavior.MaxRetries = 3
        End If

        If _config.Behavior.RetryDelaySeconds < 1 Then
            Console.WriteLine("Warning: RetryDelaySeconds must be at least 1, using default 30")
            _config.Behavior.RetryDelaySeconds = 30
        End If
    End Sub

    Private Sub CreateDefaultConfig()
        Try
            Dim defaultConfig As New Text.StringBuilder()
            defaultConfig.AppendLine("# Flashback Spooler Configuration File")
            defaultConfig.AppendLine("# Lines starting with # or ; are comments")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("[Listener]")
            defaultConfig.AppendLine("# Port 9100 is fixed for JetDirect compatibility")
            defaultConfig.AppendLine("Port9100Enabled=true")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Engine connection port (configurable)")
            defaultConfig.AppendLine("EnginePort=9001")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("[Storage]")
            defaultConfig.AppendLine("# Temporary spool directory")
            defaultConfig.AppendLine("SpoolDirectory=./spool")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Maximum spool file age in hours before cleanup")
            defaultConfig.AppendLine("MaxSpoolAge=24")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Maximum number of spool files to retain")
            defaultConfig.AppendLine("MaxSpoolFiles=1000")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("[Logging]")
            defaultConfig.AppendLine("# Log level: Trace, Debug, Info, Warning, Error")
            defaultConfig.AppendLine("LogLevel=Info")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Log file location")
            defaultConfig.AppendLine("LogFile=./logs/spooler.log")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("[Behavior]")
            defaultConfig.AppendLine("# Timeout in seconds for detecting job completion on port 9100")
            defaultConfig.AppendLine("JobCompletionTimeout=5")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Maximum job size in MB (0 = unlimited)")
            defaultConfig.AppendLine("MaxJobSizeMB=100")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Enable automatic retry for failed transmissions")
            defaultConfig.AppendLine("EnableRetry=true")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Retry attempts before giving up")
            defaultConfig.AppendLine("MaxRetries=3")
            defaultConfig.AppendLine()
            defaultConfig.AppendLine("# Retry delay in seconds")
            defaultConfig.AppendLine("RetryDelaySeconds=30")

            File.WriteAllText(_configPath, defaultConfig.ToString())
            Console.WriteLine($"Default configuration created: {_configPath}")
        Catch ex As Exception
            Console.WriteLine($"Warning: Could not create default config file: {ex.Message}")
        End Try
    End Sub
    Public ReadOnly Property Config As SpoolerConfig
        Get
            Return _config
        End Get
    End Property
End Class
