Imports System.Net
Imports System.Net.Sockets
Imports System.Threading.Tasks
Imports TN3270Framework.Events

Namespace Server
    ''' <summary>
    ''' TCP listener that accepts TN3270 client connections and raises
    ''' <see cref="ConnectionReceived"/> for each new session.
    ''' </summary>
    Public Class TN3270Listener
        Private ReadOnly _listener As TcpListener
        Private ReadOnly _port As Integer
        Private _isRunning As Boolean

        Public Event ConnectionReceived As EventHandler(Of TN3270ConnectionEventArgs)

        Public ReadOnly Property Port As Integer
            Get
                Return _port
            End Get
        End Property

        Public ReadOnly Property IsRunning As Boolean
            Get
                Return _isRunning
            End Get
        End Property

        Public Sub New(port As Integer)
            If port < 1 OrElse port > 65535 Then
                Throw New ArgumentOutOfRangeException(NameOf(port), "Port must be between 1 and 65535.")
            End If
            _port = port
            _listener = New TcpListener(IPAddress.Any, port)
        End Sub

        ''' <summary>Start accepting connections on a background task.</summary>
        Public Sub Start()
            If _isRunning Then Return
            _listener.Start()
            _isRunning = True
            Task.Run(AddressOf ListenLoop)
        End Sub

        ''' <summary>Stop accepting new connections and close the listener socket.</summary>
        Public Sub StopListening()
            _isRunning = False
            Try
                _listener.Stop()
            Catch
                ' already stopped
            End Try
        End Sub

        Private Async Sub ListenLoop()
            While _isRunning
                Try
                    Dim client = Await _listener.AcceptTcpClientAsync().ConfigureAwait(False)
                    Dim session = New TN3270Session(client)
                    Dim args As New TN3270ConnectionEventArgs(session, client.Client.RemoteEndPoint)
                    RaiseEvent ConnectionReceived(Me, args)
                Catch ex As ObjectDisposedException
                    ' Listener was stopped
                    Exit While
                Catch ex As SocketException
                    If _isRunning Then
                        ' Transient accept error; continue
                    End If
                Catch ex As Exception
                    If _isRunning Then
                        ' Unexpected; continue accepting
                    End If
                End Try
            End While
        End Sub
    End Class
End Namespace

' Root-level alias so existing code can still say New TN3270Listener(port)
Public Class TN3270Listener
    Inherits Server.TN3270Listener

    Public Sub New(port As Integer)
        MyBase.New(port)
    End Sub
End Class
