Imports TN3270Framework.Model

Namespace Events
    Public Class TN3270ConnectionEventArgs
        Inherits EventArgs

        Public ReadOnly Property Session As TN3270Session
        Public ReadOnly Property RemoteEndPoint As Net.EndPoint

        Public Sub New(session As TN3270Session, endpoint As Net.EndPoint)
            Me.Session = session
            Me.RemoteEndPoint = endpoint
        End Sub
    End Class

    Public Class DataReceivedEventArgs
        Inherits EventArgs

        Public ReadOnly Property Data As Byte()

        Public Sub New(data As Byte())
            Me.Data = data
        End Sub
    End Class

    Public Class AidKeyEventArgs
        Inherits EventArgs

        Public ReadOnly Property AidKey As Byte
        Public ReadOnly Property ActiveField As TN3270Field
        Public ReadOnly Property CursorAddress As Integer

        Public Sub New(key As Byte, activeField As TN3270Field, cursorAddress As Integer)
            Me.AidKey = key
            Me.ActiveField = activeField
            Me.CursorAddress = cursorAddress
        End Sub

        ''' <summary>Friendly name of the AID (ENTER, PF3, …).</summary>
        Public ReadOnly Property AidName As String
            Get
                Return Protocol.AidKeys.GetName(AidKey)
            End Get
        End Property
    End Class

    Public Class StructuredFieldEventArgs
        Inherits EventArgs

        Public ReadOnly Property Data As Byte()

        Public Sub New(data As Byte())
            Me.Data = data
        End Sub
    End Class
End Namespace
