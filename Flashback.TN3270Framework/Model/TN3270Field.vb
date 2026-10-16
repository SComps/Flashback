Imports TN3270Framework.Protocol

Namespace Model
    ''' <summary>
    ''' Provides the screen geometry needed to compute field buffer addresses.
    ''' Implemented by TN3270Session.
    ''' </summary>
    Public Interface IScreenGeometry
        ReadOnly Property Columns As Integer
        ReadOnly Property ScreenSize As Integer
    End Interface

    ''' <summary>
    ''' Logical representation of a 3270 screen field.
    ''' Row/Col are 1-based and refer to the first data character position;
    ''' the field attribute byte occupies the cell immediately before that position.
    ''' </summary>
    Public Class TN3270Field
        Private ReadOnly _screen As IScreenGeometry

        Public Sub New(screen As IScreenGeometry)
            If screen Is Nothing Then Throw New ArgumentNullException(NameOf(screen))
            _screen = screen
        End Sub

        ''' <summary>Optional application-level name used by GetFieldByName / GetFieldValue.</summary>
        Public Property Name As String = ""

        ''' <summary>1-based row of the first data character.</summary>
        Public Property Row As Integer

        ''' <summary>1-based column of the first data character.</summary>
        Public Property Col As Integer

        ''' <summary>Maximum number of data characters the field can hold.</summary>
        Public Property Length As Integer

        ''' <summary>Current field content (host encoding is applied at send time).</summary>
        Public Property Content As String = ""

        Public Property IsProtected As Boolean = False
        Public Property IsNumeric As Boolean = False
        Public Property Modified As Boolean = False
        Public Property Intensity As FieldIntensity = FieldIntensity.Normal

        Public Property ForegroundColor As Byte = Color.Neutral
        Public Property BackgroundColor As Byte = Color.Neutral
        Public Property Highlighting As Byte = Highlight.None
        Public Property FieldTransparency As Byte = Transparency.None

        ''' <summary>
        ''' Linear buffer address of the field attribute byte.
        ''' Data occupies Address+1 .. Address+Length.
        ''' </summary>
        Public ReadOnly Property Address As Integer
            Get
                Dim cols = _screen.Columns
                Dim totalCells = _screen.ScreenSize
                ' Attribute sits one cell before the requested data origin.
                Return (((Row - 1) * cols) + (Col - 1) - 1 + totalCells) Mod totalCells
            End Get
        End Property

        ''' <summary>
        ''' Build the base field-attribute byte (protected / numeric / intensity / MDT)
        ''' and map it onto a valid 3270 graphic character for transmission.
        ''' </summary>
        Public Function GetAttributeByte() As Byte
            Dim attr As Byte = 0

            If IsProtected Then attr = CByte(attr Or FieldAttributeBits.ProtectedField)
            If IsNumeric Then attr = CByte(attr Or FieldAttributeBits.Numeric)

            Select Case Intensity
                Case FieldIntensity.High
                    attr = CByte(attr Or FieldAttributeBits.IntensityHigh)
                Case FieldIntensity.Hidden
                    attr = CByte(attr Or FieldAttributeBits.IntensityHidden)
            End Select

            If Modified Then attr = CByte(attr Or FieldAttributeBits.ModifiedDataTag)

            Return AddressCodec.MapAttributeToGraphic(attr)
        End Function

        ''' <summary>True when any extended attribute (color, highlight, transparency) is non-default.</summary>
        Public ReadOnly Property HasExtendedAttributes As Boolean
            Get
                Return ForegroundColor <> Color.Neutral OrElse
                       BackgroundColor <> Color.Neutral OrElse
                       Highlighting <> Highlight.None OrElse
                       FieldTransparency <> Transparency.None
            End Get
        End Property
    End Class
End Namespace
