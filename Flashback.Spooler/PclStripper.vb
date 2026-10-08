Imports System
Imports System.Text

''' <summary>
''' Strips HP PCL escape sequences from a raw spool stream received from MPE/iX (JetDirect port 9100).
'''
''' MPE/iX banner pages are rendered using PCL cursor-positioning commands (ESC&a+NNR) rather than
''' plain line feeds.  The body of the job uses ordinary CR+LF.  This class converts both forms to
''' clean LF-delimited text, preserving FF (form feed) page breaks, so that Flashback.Engine receives
''' the same byte stream it would get from a plain line-printer device.
'''
''' PCL sequence grammar handled:
'''   ESC E / ESC Z / ESC =  — single-parameter two-byte sequences  → discard
'''   ESC % ...              — Universal Exit Language (UEL): ESC % [-0-9]* [A-Za-z] → discard
'''   ESC &amp; / ESC ( / ESC ) / ESC * — parameterised sequences ending on an uppercase letter
'''       ESC&a+NNR (relative row move) is the banner line-break command → emit LF instead
'''       All other parameterised sequences → discard
'''
''' Control-character mapping:
'''   CR followed by ESC&a (row-move) → discard CR; row-move emits the LF (avoids double-spacing)
'''   CR alone (not followed by LF)   → LF
'''   CR + LF                         → LF   (already proper; CR discarded)
'''   FF (0x0C)                       → FF   (page break, kept)
'''   All other C0 controls (&lt; 0x20, not LF/FF) → discard
''' </summary>
Public Class PclStripper

    ''' <summary>
    ''' Process <paramref name="input"/> bytes in-place and return the cleaned byte array.
    ''' The input is treated as Latin-1 (ISO 8859-1); output is also Latin-1.
    ''' </summary>
    Public Shared Function Strip(input As Byte()) As Byte()
        Dim out As New List(Of Byte)(input.Length)
        Dim i As Integer = 0

        While i < input.Length
            Dim b As Byte = input(i)

            If b = &H1B Then            ' ESC — start of PCL sequence
                Dim isRowMove As Boolean = False
                i += 1
                If i >= input.Length Then Exit While

                Dim kind As Byte = input(i)  ' character after ESC

                If kind = AscW("E") OrElse kind = AscW("Z") OrElse kind = AscW("=") OrElse kind = AscW("Y") OrElse kind = AscW("9") Then
                    ' Simple two-byte sequence: ESC E, ESC Z, etc. — discard both bytes
                    i += 1

                ElseIf kind = AscW("%") OrElse kind = AscW("&") OrElse
                       kind = AscW("(") OrElse kind = AscW(")") OrElse
                       kind = AscW("*") Then
                    ' Parameterised sequence.
                    ' For ESC& the group character follows immediately, then numeric params,
                    ' then a terminating uppercase letter.
                    ' Detect ESC&a...R (row move) before consuming.
                    i += 1  ' skip kind byte

                    ' Peek: for ESC& the next byte is the group char (e.g. 'a', 'l', 'k', 'd')
                    ' For ESC&a+NNR we want to emit a LF.
                    Dim groupChar As Byte = 0
                    If kind = AscW("&") AndAlso i < input.Length Then
                        groupChar = input(i)
                    End If

                    ' Consume through the terminating character.
                    ' PCL parameterised sequences end on an uppercase letter [A-Z].
                    ' Exception: ESC&d uses '@' (0x40) as its sole terminator value.
                    ' PCL combination sequences use lowercase for intermediate params and
                    ' uppercase for the final one — we scan to the first uppercase or '@'.
                    Dim terminatorByte As Byte = 0
                    While i < input.Length
                        Dim pb As Byte = input(i)
                        i += 1
                        If pb = AscW("@") OrElse (pb >= AscW("A") AndAlso pb <= AscW("Z")) Then
                            terminatorByte = pb
                            Exit While
                        End If
                    End While

                    ' ESC&a...R  →  row move  →  treat as line break
                    If kind = AscW("&") AndAlso groupChar = AscW("a") AndAlso terminatorByte = AscW("R") Then
                        out.Add(&H0A)   ' LF
                    End If
                    ' All other parameterised sequences are discarded (already consumed above)
                Else
                    ' Unknown ESC sequence — skip the ESC and let the next byte be re-evaluated
                    ' (don't advance i again; we already moved past ESC)
                End If

            ElseIf b = &H0D Then        ' CR
                ' Peek ahead: if the next non-LF byte is ESC&a (a row-move sequence),
                ' suppress this CR — the row-move itself will emit the LF, and emitting
                ' one here too would produce unwanted double-spacing on banner lines.
                Dim peekIdx As Integer = i + 1
                If peekIdx < input.Length AndAlso input(peekIdx) = &H0A Then peekIdx += 1  ' skip LF if CR+LF
                Dim followedByRowMove As Boolean = (peekIdx + 2 < input.Length AndAlso
                                                    input(peekIdx) = &H1B AndAlso
                                                    input(peekIdx + 1) = AscW("&") AndAlso
                                                    input(peekIdx + 2) = AscW("a"))
                If followedByRowMove Then
                    ' Discard CR (and LF if present) — the row-move provides the line break
                    If i + 1 < input.Length AndAlso input(i + 1) = &H0A Then
                        i += 2
                    Else
                        i += 1
                    End If
                ElseIf i + 1 < input.Length AndAlso input(i + 1) = &H0A Then
                    ' CR+LF — emit single LF, skip both
                    out.Add(&H0A)
                    i += 2
                Else
                    ' Bare CR — treat as LF
                    out.Add(&H0A)
                    i += 1
                End If

            ElseIf b = &H0A Then        ' LF — pass through
                out.Add(b)
                i += 1

            ElseIf b = &H0C Then        ' FF — pass through
                out.Add(b)
                i += 1

            ElseIf b < &H20 Then        ' Other control characters — discard
                i += 1

            Else                        ' Printable byte — pass through
                out.Add(b)
                i += 1
            End If
        End While

        Return out.ToArray()
    End Function

End Class
