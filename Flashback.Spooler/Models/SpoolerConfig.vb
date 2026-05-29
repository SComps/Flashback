Imports System

Namespace Models
    ''' <summary>
    ''' Complete configuration for the Flashback Spooler service
    ''' </summary>
    Public Class SpoolerConfig
        Public Property Listener As ListenerConfig
        Public Property Storage As StorageConfig
        Public Property Logging As LoggingConfig
        Public Property Behavior As BehaviorConfig

        Public Sub New()
            Listener = New ListenerConfig()
            Storage = New StorageConfig()
            Logging = New LoggingConfig()
            Behavior = New BehaviorConfig()
        End Sub
    End Class

    ''' <summary>
    ''' Configuration for network listeners
    ''' </summary>
    Public Class ListenerConfig
        ''' <summary>
        ''' Enable the port 9100 listener (JetDirect compatible)
        ''' </summary>
        Public Property Port9100Enabled As Boolean = True

        ''' <summary>
        ''' Port for Flashback.Engine to connect to
        ''' </summary>
        Public Property EnginePort As Integer = 9001

        Public Sub New()
        End Sub
    End Class

    ''' <summary>
    ''' Configuration for spool file storage
    ''' </summary>
    Public Class StorageConfig
        ''' <summary>
        ''' Directory path for temporary spool files
        ''' </summary>
        Public Property SpoolDirectory As String = "./spool"

        ''' <summary>
        ''' Maximum age of spool files in hours before cleanup
        ''' </summary>
        Public Property MaxSpoolAge As Integer = 24

        ''' <summary>
        ''' Maximum number of spool files to retain
        ''' </summary>
        Public Property MaxSpoolFiles As Integer = 1000

        Public Sub New()
        End Sub
    End Class

    ''' <summary>
    ''' Configuration for logging
    ''' </summary>
    Public Class LoggingConfig
        ''' <summary>
        ''' Log level: Trace, Debug, Info, Warning, Error
        ''' </summary>
        Public Property LogLevel As String = "Info"

        ''' <summary>
        ''' Path to log file
        ''' </summary>
        Public Property LogFile As String = "./logs/spooler.log"

        Public Sub New()
        End Sub
    End Class

    ''' <summary>
    ''' Configuration for service behavior
    ''' </summary>
    Public Class BehaviorConfig
        ''' <summary>
        ''' Timeout in seconds for detecting job completion on port 9100
        ''' </summary>
        Public Property JobCompletionTimeout As Integer = 5

        ''' <summary>
        ''' Maximum job size in MB (0 = unlimited)
        ''' </summary>
        Public Property MaxJobSizeMB As Integer = 100

        ''' <summary>
        ''' Enable automatic retry for failed transmissions
        ''' </summary>
        Public Property EnableRetry As Boolean = True

        ''' <summary>
        ''' Maximum retry attempts before giving up
        ''' </summary>
        Public Property MaxRetries As Integer = 3

        ''' <summary>
        ''' Delay in seconds between retry attempts
        ''' </summary>
        Public Property RetryDelaySeconds As Integer = 30

        Public Sub New()
        End Sub
    End Class
End Namespace

' Made with Bob
