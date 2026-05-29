Imports System

Namespace Models
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

    Public Class ListenerConfig
        Public Property Port9100Enabled As Boolean = True
        Public Property EnginePort As Integer = 9001

        Public Sub New()
        End Sub
    End Class

    Public Class StorageConfig
        Public Property SpoolDirectory As String = "./spool"
        Public Property MaxSpoolAge As Integer = 24
        Public Property MaxSpoolFiles As Integer = 1000

        Public Sub New()
        End Sub
    End Class

    Public Class LoggingConfig
        Public Property LogLevel As String = "Info"
        Public Property LogFile As String = "./logs/spooler.log"

        Public Sub New()
        End Sub
    End Class

    Public Class BehaviorConfig
        Public Property JobCompletionTimeout As Integer = 5
        Public Property MaxJobSizeMB As Integer = 100
        Public Property EnableRetry As Boolean = True
        Public Property MaxRetries As Integer = 3
        Public Property RetryDelaySeconds As Integer = 30

        Public Sub New()
        End Sub
    End Class
End Namespace
