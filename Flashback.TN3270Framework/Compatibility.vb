Imports TN3270Framework.Protocol
Imports TN3270Framework.Model
Imports TN3270Framework.Events

' -----------------------------------------------------------------------------
' Backward-compatibility surface
' Keeps existing Flashback.Config.3270 (and any other callers) compiling while
' the library is modularised. New code should prefer the Protocol / Model /
' Events namespaces directly.
' -----------------------------------------------------------------------------

''' <summary>Legacy alias — prefer Protocol.Color.</summary>
Public NotInheritable Class TN3270Color
    Private Sub New()
    End Sub
    Public Const Neutral As Byte = Color.Neutral
    Public Const Blue As Byte = Color.Blue
    Public Const Red As Byte = Color.Red
    Public Const Pink As Byte = Color.Pink
    Public Const Green As Byte = Color.Green
    Public Const Turquoise As Byte = Color.Turquoise
    Public Const Yellow As Byte = Color.Yellow
    Public Const White As Byte = Color.White
End Class

''' <summary>Legacy alias — prefer Protocol.Highlight.</summary>
Public NotInheritable Class TN3270Highlight
    Private Sub New()
    End Sub
    Public Const None As Byte = Highlight.None
    Public Const Blink As Byte = Highlight.Blink
    Public Const ReverseVideo As Byte = Highlight.ReverseVideo
    Public Const Underline As Byte = Highlight.Underline
End Class

''' <summary>Legacy alias — prefer Protocol.Transparency.</summary>
Public NotInheritable Class TN3270Transparency
    Private Sub New()
    End Sub
    Public Const None As Byte = Transparency.None
    Public Const Transparent As Byte = Transparency.Transparent
    Public Const Opaque As Byte = Transparency.Opaque
End Class

''' <summary>Legacy alias — prefer Protocol.FieldIntensity.</summary>
Public Enum TN3270Intensity
    Normal = FieldIntensity.Normal
    High = FieldIntensity.High
    Hidden = FieldIntensity.Hidden
End Enum

''' <summary>Legacy alias — prefer Protocol.Orders.</summary>
Public NotInheritable Class ORDER
    Private Sub New()
    End Sub
    Public Const SBA As Byte = Orders.SetBufferAddress
    Public Const SF As Byte = Orders.StartField
    Public Const SFE As Byte = Orders.StartFieldExtended
    Public Const IC As Byte = Orders.InsertCursor
    Public Const SA As Byte = Orders.SetAttribute
    Public Const PT As Byte = Orders.ProgramTab
    Public Const RA As Byte = Orders.RepeatToAddress
    Public Const EUA As Byte = Orders.EraseUnprotectedToAddress
    Public Const GE As Byte = Orders.GraphicEscape
    Public Const MF As Byte = Orders.ModifyField
End Class

''' <summary>Legacy alias — prefer Protocol.Commands.</summary>
Public NotInheritable Class INTERFACE_CMD
    Private Sub New()
    End Sub
    Public Const WRITE As Byte = Commands.Write
    Public Const ERASE_WRITE As Byte = Commands.EraseWrite
    Public Const ERASE_WRITE_ALTERNATE As Byte = Commands.EraseWriteAlternate
    Public Const WSF As Byte = Commands.WriteStructuredField
    Public Const READ_BUFFER As Byte = Commands.ReadBuffer
    Public Const READ_MODIFIED As Byte = Commands.ReadModified
    Public Const READ_MODIFIED_ALL As Byte = Commands.ReadModifiedAll
    Public Const ERASE_ALL_UNPROTECTED As Byte = Commands.EraseAllUnprotected
    Public Const NOP As Byte = Commands.Nop
End Class

''' <summary>Legacy alias — prefer Protocol.AidKeys.</summary>
Public NotInheritable Class AID_KEYS
    Private Sub New()
    End Sub
    Public Const NONE As Byte = AidKeys.None
    Public Const ENTER As Byte = AidKeys.Enter
    Public Const CLEAR As Byte = AidKeys.Clear
    Public Const PA1 As Byte = AidKeys.PA1
    Public Const PA2 As Byte = AidKeys.PA2
    Public Const PA3 As Byte = AidKeys.PA3
    Public Const SYSREQ As Byte = AidKeys.SysReq
    Public Const STRUCTURED_FIELD As Byte = AidKeys.StructuredField
    Public Const PF1 As Byte = AidKeys.PF1
    Public Const PF2 As Byte = AidKeys.PF2
    Public Const PF3 As Byte = AidKeys.PF3
    Public Const PF4 As Byte = AidKeys.PF4
    Public Const PF5 As Byte = AidKeys.PF5
    Public Const PF6 As Byte = AidKeys.PF6
    Public Const PF7 As Byte = AidKeys.PF7
    Public Const PF8 As Byte = AidKeys.PF8
    Public Const PF9 As Byte = AidKeys.PF9
    Public Const PF10 As Byte = AidKeys.PF10
    Public Const PF11 As Byte = AidKeys.PF11
    Public Const PF12 As Byte = AidKeys.PF12
    Public Const PF13 As Byte = AidKeys.PF13
    Public Const PF14 As Byte = AidKeys.PF14
    Public Const PF15 As Byte = AidKeys.PF15
    Public Const PF16 As Byte = AidKeys.PF16
    Public Const PF17 As Byte = AidKeys.PF17
    Public Const PF18 As Byte = AidKeys.PF18
    Public Const PF19 As Byte = AidKeys.PF19
    Public Const PF20 As Byte = AidKeys.PF20
    Public Const PF21 As Byte = AidKeys.PF21
    Public Const PF22 As Byte = AidKeys.PF22
    Public Const PF23 As Byte = AidKeys.PF23
    Public Const PF24 As Byte = AidKeys.PF24

    Public Shared Function GetKeyName(aid As Byte) As String
        Return AidKeys.GetName(aid)
    End Function
End Class

''' <summary>Legacy alias — prefer Protocol.CharacterSets.</summary>
Public NotInheritable Class TN3270CharacterSets
    Private Sub New()
    End Sub
    Public Shared Function GetEncoding(codePage As String) As Text.Encoding
        Return CharacterSets.GetEncoding(codePage)
    End Function
    Public Shared Function GetCodePageName(codePage As Integer) As String
        Return CharacterSets.GetCodePageName(codePage)
    End Function
End Class

''' <summary>Legacy alias — prefer Protocol.AddressCodec / Protocol.FieldAttributeBits.</summary>
Public NotInheritable Class TN3270Utils
    Private Sub New()
    End Sub
    Public Shared ReadOnly Property AddressCodeMap As Byte()
        Get
            Return AddressCodec.CodeMap
        End Get
    End Property
    Public Shared Function MapAttributeToGraphic(attr As Byte) As Byte
        Return AddressCodec.MapAttributeToGraphic(attr)
    End Function
End Class

''' <summary>Legacy alias — prefer Protocol.FieldAttributeBits.</summary>
Public NotInheritable Class TN3270Attribute
    Private Sub New()
    End Sub
    Public Const ProtectedField As Byte = FieldAttributeBits.ProtectedField
    Public Const Numeric As Byte = FieldAttributeBits.Numeric
    Public Const IntensityHigh As Byte = FieldAttributeBits.IntensityHigh
    Public Const IntensityHidden As Byte = FieldAttributeBits.IntensityHidden
    Public Const MDT As Byte = FieldAttributeBits.ModifiedDataTag
End Class

' Re-export event-arg types at root for existing Imports TN3270Framework consumers
Public Class TN3270ConnectionEventArgs
    Inherits Events.TN3270ConnectionEventArgs
    Public Sub New(session As TN3270Session, endpoint As Net.EndPoint)
        MyBase.New(session, endpoint)
    End Sub
End Class

Public Class DataReceivedEventArgs
    Inherits Events.DataReceivedEventArgs
    Public Sub New(data As Byte())
        MyBase.New(data)
    End Sub
End Class

Public Class AidKeyEventArgs
    Inherits Events.AidKeyEventArgs
    Public Sub New(key As Byte, activeField As Model.TN3270Field, cursorAddress As Integer)
        MyBase.New(key, activeField, cursorAddress)
    End Sub
End Class

Public Class StructuredFieldEventArgs
    Inherits Events.StructuredFieldEventArgs
    Public Sub New(data As Byte())
        MyBase.New(data)
    End Sub
End Class
