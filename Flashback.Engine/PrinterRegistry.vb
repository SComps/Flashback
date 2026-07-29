Imports Flashback.Core

''' <summary>
''' Singleton shared between Worker and WebWorker.
''' Worker registers/unregisters live Devs objects; WebWorker reads snapshots for status display.
''' </summary>
Public Class PrinterRegistry
    Private ReadOnly _lock As New Object
    Private ReadOnly _devices As New List(Of Devs)

    ''' <summary>Add a device to the registry.</summary>
    Public Sub Register(d As Devs)
        SyncLock _lock
            If Not _devices.Contains(d) Then
                _devices.Add(d)
            End If
        End SyncLock
    End Sub

    ''' <summary>Remove a device from the registry.</summary>
    Public Sub Unregister(d As Devs)
        SyncLock _lock
            _devices.Remove(d)
        End SyncLock
    End Sub

    ''' <summary>Returns a shallow copy of the current device list. Safe to iterate outside the lock.</summary>
    Public Function GetSnapshot() As IReadOnlyList(Of Devs)
        SyncLock _lock
            Return New List(Of Devs)(_devices)
        End SyncLock
    End Function
End Class
