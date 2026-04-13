Imports System.IO
Imports System.Text.RegularExpressions
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
    Public Property TypeFaceName As String = "Chainprinter"
    Public Property CustomFontPath As String = "ibmplexmono.ttf"
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
                GlobalFontSettings.FontResolver = New DynamicFontResolver()
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
                If TypeFaceName = "Chainprinter" AndAlso profile.DefaultFont <> "" Then
                    TypeFaceName = profile.DefaultFont
                End If
            End If

            Dim leftMargin As Double = 40
            Dim rightMargin As Double = 40
            Dim availableWidth As Double
            Dim fontSize As Double
            Dim font As XFont = Nothing
            Dim page As PdfPage = Nothing
            Dim gfx As XGraphics = Nothing
            Dim y As Double = firstline
            Dim currentLine As Integer = StartLine
            Dim lineHeight As Double

            Dim InitializeNewPage = Sub()
                                        page = doc.AddPage()
                                        lineHeight = 12
                                        page.Width = XUnit.FromInch(14.875)
                                        page.Height = XUnit.FromInch(11)

                                        If Orientation <= 1 Then
                                            page.Orientation = PdfSharp.PageOrientation.Landscape
                                        Else
                                            page.Orientation = PdfSharp.PageOrientation.Portrait
                                            Dim tempWidth As XUnit = page.Width
                                            page.Width = page.Height
                                            page.Height = tempWidth
                                        End If

                                        gfx = XGraphics.FromPdfPage(page)

                                        If (Orientation = 0) Or (Orientation = 2) Then
                                            DrawGreenBarBackground(gfx, page.Width.Point, page.Height.Point)
                                        End If

                                        availableWidth = page.Width.Point - leftMargin - rightMargin
                                        font = New XFont(TypeFaceName, 12)
                                        Dim charWidth As Double = gfx.MeasureString("W", font).Width
                                        If Orientation <= 1 Then
                                            fontSize = availableWidth / (charWidth * 132) * 12
                                        Else
                                            fontSize = availableWidth / (charWidth * 80) * 12
                                        End If
                                        font = New XFont(TypeFaceName, fontSize)
                                        y = firstline
                                        currentLine = 0
                                    End Sub

            InitializeNewPage()

            Dim regex As New Regex("[^\x20-\x7E\x0C\x0D\u00A0]", RegexOptions.Compiled)
            Dim mperegex As New Regex("[^\x20-\x7E\x0C]", RegexOptions.Compiled)
            If outList.Count > 0 AndAlso outList(0).Trim = "" Then
                outList.RemoveAt(0)
            End If

            For Each line As String In outList
                Try
                    If ((OS <> OSType.OS_RSTS) And (OS <> OSType.OS_MPE)) Then
                        line = regex.Replace(line, String.Empty)
                        line = If(String.IsNullOrEmpty(line), " ", line)
                    End If

                    If line.StartsWith(vbFormFeed) Then
                        InitializeNewPage()
                    End If

                    If (currentLine >= linesPerPage) Then
                        InitializeNewPage()
                    End If

                    If line <> vbFormFeed Then
                        If Orientation > 1 Then
                            If line.Length > 80 Then line = line.Substring(0, 80)
                        Else
                            If line.Length > 132 Then line = line.Substring(0, 132)
                        End If

                        If line.Contains(Chr(13)) Then
                            Dim segments As List(Of String) = line.Split(Chr(13)).ToList()
                            Dim currentX As Double = leftMargin
                            If (segments.Count > 1) And (segments(1).Trim <> "") Then
                                Dim segIdx As Integer = 0
                                For Each segment As String In segments
                                    If Not String.IsNullOrEmpty(segment) Then
                                        gfx.DrawString(segment, font, XBrushes.Black, New XRect(currentX, y, availableWidth, page.Height.Point), XStringFormats.TopLeft)
                                        If segIdx > 0 Then
                                            gfx.DrawString(segment, font, XBrushes.Black, New XRect(currentX, y, availableWidth, page.Height.Point), XStringFormats.TopLeft)
                                        End If
                                        segIdx += 1
                                    End If
                                Next
                            Else
                                gfx.DrawString(line, font, XBrushes.Black, New XRect(leftMargin, y, availableWidth, page.Height.Point), XStringFormats.TopLeft)
                            End If
                        Else
                            gfx.DrawString(line, font, XBrushes.Black, New XRect(leftMargin, y, availableWidth, page.Height.Point), XStringFormats.TopLeft)
                        End If

                        y += lineHeight
                        currentLine += 1
                    End If
                Catch ex As Exception
                    Logger?.LogError("{Dev}: Error processing line: {Error}", DevName, ex.Message)
                End Try
            Next

            Dim outputFile As String = TargetFileName
            Logger?.LogInformation("{Dev}: Wrote {Pages} pages for {Job} to {File}.", DevName, doc.PageCount, title, outputFile)
            doc.Save(outputFile)
            doc.Close()
            Return outputFile
        Catch ex As Exception
            Logger?.LogError("{Dev}: Error in CreatePDF: {Error}", DevName, ex.Message)
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

    Public Sub OldDrawBackgroundTemplate(gfx As XGraphics, drawBG As Boolean, dark As XColor, light As XColor)
        Const feedHoleRadius As Double = 5.5
        Dim pageWidth As Double = gfx.PageSize.Width
        Dim pageHeight As Double = gfx.PageSize.Height

        If drawBG Then
            Dim darkPen As New XPen(dark, 0.7)
            gfx.DrawLine(darkPen, 20, 54 - feedHoleRadius * 2, 20, 54 + feedHoleRadius * 2)
            gfx.DrawLine(darkPen, 20 - feedHoleRadius * 2, 54, 20 + feedHoleRadius * 2, 54)
            darkPen.Width = 1.5
            gfx.DrawEllipse(darkPen, 20 - (feedHoleRadius + 0.6), 54 - (feedHoleRadius + 0.6), (feedHoleRadius + 0.6) * 2, (feedHoleRadius + 0.6) * 2)
        End If

        Dim grayPen As New XPen(XColors.LightGray, 0.75)
        Dim lightGrayBrush As New XSolidBrush(XColor.FromArgb(230, 230, 230))
        gfx.DrawEllipse(grayPen, lightGrayBrush, 20 - (feedHoleRadius + 1), 18 - (feedHoleRadius + 1), (feedHoleRadius + 1) * 2, (feedHoleRadius + 1) * 2)
        gfx.DrawEllipse(grayPen, lightGrayBrush, pageWidth - 20 - (feedHoleRadius + 1), 18 - (feedHoleRadius + 1), (feedHoleRadius + 1) * 2, (feedHoleRadius + 1) * 2)
        Dim y As Double
        For i As Integer = 1 To 21
            y = 18 + 18 * 2 * i
            gfx.DrawEllipse(grayPen, lightGrayBrush, 20 - feedHoleRadius, y - feedHoleRadius, feedHoleRadius * 2, feedHoleRadius * 2)
            gfx.DrawEllipse(grayPen, lightGrayBrush, pageWidth - 20 - feedHoleRadius, y - feedHoleRadius, feedHoleRadius * 2, feedHoleRadius * 2)
        Next

        If Not drawBG Then Exit Sub

        gfx.DrawPolygon(New XPen(XColors.Transparent), New XSolidBrush(light), New XPoint() {
            New XPoint(40 + 2, 72 - 11),
            New XPoint(40 + 2 + 5, 72),
            New XPoint(40 + 2 + 10, 72 - 11)
        }, XFillMode.Winding)

        gfx.DrawPolygon(New XPen(XColors.Transparent), New XSolidBrush(light), New XPoint() {
            New XPoint(pageWidth - 40 - 2, 72 - 11),
            New XPoint(pageWidth - 40 - 2 - 5, 72),
            New XPoint(pageWidth - 40 - 2 - 10, 72 - 11)
        }, XFillMode.Winding)

        Dim barHeight As Double = 36
        Dim barCount As Integer = CInt(pageHeight / barHeight) + 1
        For i As Integer = 0 To barCount - 1
            Dim yPos As Double = 72 + (i * barHeight)
            Dim brush As XSolidBrush
            If i Mod 2 = 0 Then
                brush = New XSolidBrush(XColor.FromArgb(220, 255, 220))
            Else
                brush = New XSolidBrush(XColor.FromArgb(255, 255, 255))
            End If
            gfx.DrawRectangle(brush, 40, yPos, pageWidth - 80, barHeight)
        Next

        Dim darkPenVertical As New XPen(dark, 0.5)
        gfx.DrawLine(darkPenVertical, 30, 72, 30, pageHeight - 1)
        gfx.DrawLine(darkPenVertical, 40, 72, 40, pageHeight - 1)
        gfx.DrawLine(darkPenVertical, pageWidth - 30, 72, pageWidth - 30, pageHeight - 1)
        gfx.DrawLine(darkPenVertical, pageWidth - 40, 72, pageWidth - 40, pageHeight - 1)

        Dim font As New XFont("C:\Windows\Fonts\segoeui.ttf", 7)
        gfx.DrawString("1", font, New XSolidBrush(dark), New XPoint(30, 72))
        For i As Integer = 1 To 60
            gfx.DrawString((i + 1).ToString(), font, New XSolidBrush(dark), New XPoint(30, 72 + (i * 12)))
        Next

        gfx.DrawString("1", font, New XSolidBrush(dark), New XPoint(pageWidth - 40, 72))
        For i As Integer = 1 To 80
            gfx.DrawString((i + 1).ToString(), font, New XSolidBrush(dark), New XPoint(pageWidth - 40, 72 + (i * 9)))
        Next
    End Sub
End Class
