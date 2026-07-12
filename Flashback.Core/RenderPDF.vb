Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Threading
Imports PdfSharp.Drawing
Imports PdfSharp.Fonts
Imports PdfSharp.Pdf
Imports Microsoft.Extensions.Logging

Public Enum OSType
    OS_MVS38J
    OS_VMS
    OS_MPE
    OS_RSTS
    OS_VM370
    OS_NOS278
    OS_VMSP
    OS_TANDYXENIX
    OS_ZOS
    OS_ZVM73
    OS_GENERIC
End Enum

Public Class DynamicFontResolver
    Implements IFontResolver

    Private ReadOnly _fonts As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public Sub RegisterFont(familyName As String, path As String)
        _fonts(familyName) = path
    End Sub

    Public Function GetFont(faceName As String) As Byte() Implements IFontResolver.GetFont
        If _fonts.ContainsKey(faceName) Then
            Dim path As String = _fonts(faceName)
            If File.Exists(path) Then Return File.ReadAllBytes(path)
        End If
        
        Dim currentDirFallback = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, faceName & ".ttf")
        If File.Exists(currentDirFallback) Then Return File.ReadAllBytes(currentDirFallback)

        Throw New FileNotFoundException($"Could not find font for face '{faceName}'.")
    End Function

    Public Function ResolveTypeface(familyName As String, isBold As Boolean, isItalic As Boolean) As FontResolverInfo Implements IFontResolver.ResolveTypeface
        Return New FontResolverInfo(familyName)
    End Function
End Class

Public Class RenderPDF
    ' Lock object used to serialise the one-time FontResolver initialisation across threads
    Private Shared ReadOnly _fontResolverLock As New Object()

    Public Enum ShadingColor
        Green
        Blue
        None
    End Enum

    Public Property OS As OSType = OSType.OS_MVS38J
    Public Property Orientation As Integer = 0
    Public Property DevName As String = "Printer"
    Public Property TargetFileName As String = "out.pdf"
    Public Property Shading As ShadingColor = ShadingColor.Green
    Public Property TypeFaceName As String = "OCR-B"
    Public Property CustomFontPath As String = "OCR-B.ttf"
    Public Property Logger As Microsoft.Extensions.Logging.ILogger

    Public Function CreatePDF(title As String, outList As List(Of String)) As String
        Try
            Logger?.LogInformation("{Dev}: beginning PDF generation.", DevName)
            Dim firstline As Double = 0
            Dim linesPerPage As Integer = 66
            Dim StartLine = 0
            Dim doc As New PdfDocument()

            ' Ensure font resolver is set only once
            If GlobalFontSettings.FontResolver Is Nothing Then
                SyncLock _fontResolverLock
                    If GlobalFontSettings.FontResolver Is Nothing Then
                        GlobalFontSettings.FontResolver = New DynamicFontResolver()
                    End If
                End SyncLock
            End If

            If TypeOf GlobalFontSettings.FontResolver Is DynamicFontResolver Then
                Dim resolver = DirectCast(GlobalFontSettings.FontResolver, DynamicFontResolver)
                resolver.RegisterFont(TypeFaceName, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CustomFontPath))
            End If

            doc.Info.Title = title

            Dim profile As IOsProfile = OsProfileFactory.GetProfile(OS)
            If profile IsNot Nothing Then
                firstline = profile.FirstLine
                linesPerPage = profile.LinesPerPage
                StartLine = profile.StartLine
                If TypeFaceName = "OCR-B" AndAlso profile.DefaultFont <> "" Then
                    TypeFaceName = profile.DefaultFont
                End If
            End If

            Dim leftMargin As Double = 40
            Dim rightMargin As Double = 40
            Dim availableWidth As Double
            Dim font As XFont = Nothing
            Dim page As PdfPage = Nothing
            Dim gfx As XGraphics = Nothing
            Dim lineHeight As Double = 12

            ' Virtual Print Head State
            Dim currentX As Double = leftMargin
            Dim currentY As Double = firstline

            Dim InitializeNewPage = Sub()
                                        page = doc.AddPage()
                                        If Orientation <= 1 Then
                                            page.Orientation = PdfSharp.PageOrientation.Landscape
                                            page.Width = XUnit.FromInch(14.875)
                                            page.Height = XUnit.FromInch(11)
                                        Else
                                            page.Orientation = PdfSharp.PageOrientation.Portrait
                                            page.Width = XUnit.FromInch(8.5)
                                            page.Height = XUnit.FromInch(11)
                                        End If

                                        gfx = XGraphics.FromPdfPage(page)
                                        If (Orientation = 0) Or (Orientation = 2) Then
                                            DrawGreenBarBackground(gfx, page.Width.Point, page.Height.Point)
                                        End If

                                        availableWidth = page.Width.Point - leftMargin - rightMargin
                                        Dim targetCharsPerLine As Integer = If(Orientation <= 1, 132, 80)
                                        Dim testFont As XFont = New XFont(TypeFaceName, 12, XFontStyleEx.Regular)
                                        Dim testWidth As Double = gfx.MeasureString(New String("M"c, targetCharsPerLine), testFont).Width
                                        Dim fontSize As Double = (availableWidth / testWidth) * 12
                                        font = New XFont(TypeFaceName, fontSize, XFontStyleEx.Regular)

                                        currentX = leftMargin
                                        currentY = firstline
                                    End Sub

            InitializeNewPage()

            ' Process every character in the stream
            For Each line As String In outList
                For Each c As Char In line
                    Select Case c
                        Case ControlChars.FormFeed
                            InitializeNewPage()
                        Case ControlChars.Cr
                            currentX = leftMargin
                        Case ControlChars.Lf
                            currentY += lineHeight
                        Case Else
                            gfx.DrawString(c.ToString(), font, XBrushes.Black,
                                           New XRect(currentX, currentY, availableWidth, page.Height.Point),
                                           XStringFormats.TopLeft)
                            currentX += gfx.MeasureString(c.ToString(), font).Width
                    End Select
                Next
            Next

            Dim outputFile As String = TargetFileName
            Logger?.LogInformation("{Dev}: Wrote {Pages} pages for {Job} to {File}.", DevName, doc.PageCount, title, outputFile)
            doc.Save(outputFile)
            doc.Close()
            Return outputFile
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                Logger?.LogError("{Dev}: Error in CreatePDF: {Error}", DevName, ex.Message)
            End If
        End Try
        Return ""
    End Function

    Public Sub DrawGreenBarBackground(ByVal gfx As XGraphics, ByVal pageWidth As Double, ByVal pageHeight As Double)
        Dim paperWhite As XColor = XColors.White
        Dim bandColor As XColor = XColor.FromArgb(215, 240, 215)
        If Shading = ShadingColor.Blue Then bandColor = XColor.FromArgb(215, 225, 240)
        Dim markerGray As XColor = XColor.FromArgb(180, 180, 180)
        Dim holeShadow As XColor = XColor.FromArgb(235, 235, 235)

        Dim tractorWidth As Double = 30
        Dim marginSpace As Double = 10
        Dim contentStart As Double = tractorWidth + marginSpace
        Dim contentWidth As Double = pageWidth - (contentStart * 2)
        Dim bandHeight As Double = 36
        Dim lpi6 As Double = 12

        gfx.DrawRectangle(New XSolidBrush(paperWhite), 0, 0, pageWidth, pageHeight)

        Dim currentY As Double = 72
        Dim bandNum As Integer = 1
        While currentY < (pageHeight - 1)
            If bandNum Mod 2 = 1 AndAlso Shading <> ShadingColor.None Then
                gfx.DrawRectangle(New XSolidBrush(bandColor), contentStart, currentY, contentWidth, bandHeight)
            End If
            currentY += bandHeight
            bandNum += 1
        End While

        Dim linePen As New XPen(markerGray, 0.5)
        gfx.DrawLine(linePen, contentStart, 0, contentStart, pageHeight)
        gfx.DrawLine(linePen, contentStart + contentWidth, 0, contentStart + contentWidth, pageHeight)
        gfx.DrawLine(linePen, tractorWidth, 0, tractorWidth, pageHeight)
        gfx.DrawLine(linePen, pageWidth - tractorWidth, 0, pageWidth - tractorWidth, pageHeight)

        Dim marginFont As New XFont(TypeFaceName, 5.5)
        Dim marginBrush As New XSolidBrush(markerGray)

        For i As Integer = 1 To 60
            Dim physicalLine As Integer = 6 + i
            Dim yPos As Double = (physicalLine - 1) * lpi6
            gfx.DrawString(i.ToString(), marginFont, marginBrush,
                          New XRect(tractorWidth, yPos, marginSpace, lpi6), XStringFormats.Center)
            gfx.DrawString(i.ToString(), marginFont, marginBrush,
                          New XRect(pageWidth - tractorWidth - marginSpace, yPos, marginSpace, lpi6), XStringFormats.Center)
        Next

        Dim holeRadius As Double = 4.5
        Dim centerLine As Double = tractorWidth / 2

        For y As Double = 18 To pageHeight Step 36
            gfx.DrawEllipse(linePen, New XSolidBrush(holeShadow),
                            centerLine - holeRadius, y - holeRadius, holeRadius * 2, holeRadius * 2)
            gfx.DrawEllipse(linePen, New XSolidBrush(holeShadow),
                            pageWidth - centerLine - holeRadius, y - holeRadius, holeRadius * 2, holeRadius * 2)
        Next

        Dim markX As Double = centerLine
        Dim markY As Double = 18
        Dim sz As Double = 9

        gfx.DrawLine(linePen, markX - sz, markY, markX + sz, markY)
        gfx.DrawLine(linePen, markX, markY - sz, markX, markY + sz)
        gfx.DrawEllipse(linePen, markX - (sz * 0.8), markY - (sz * 0.8), sz * 1.6, sz * 1.6)
        Dim dia As XPoint() = {
            New XPoint(markX, markY - sz), New XPoint(markX + sz, markY),
            New XPoint(markX, markY + sz), New XPoint(markX - sz, markY)
        }
        gfx.DrawPolygon(linePen, dia)
    End Sub

End Class
