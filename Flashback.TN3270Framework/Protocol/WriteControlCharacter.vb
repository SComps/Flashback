Namespace Protocol
    ''' <summary>
    ''' Builds a 3270 Write Control Character (WCC).
    ''' Bit layout (as commonly implemented for TN3270):
    '''   bits 0-1: reserved / reset parity style (often 11b for "no reset")
    '''   bit 2:    Start Printer (print bit) — rarely used in pure display apps
    '''   bit 3:    Sound Alarm
    '''   bit 4:    Keyboard Restore
    '''   bit 5:    Reset MDT
    '''   bits 6-7: often treated as part of the graphic mapping when sent as a data byte
    '''
    ''' In practice TN3270 hosts send a single WCC byte immediately after the command
    ''' (Erase/Write, Write, etc.). This builder exposes the semantic flags and produces
    ''' a byte compatible with existing emulators.
    ''' </summary>
    Public Class WriteControlCharacter
        Public Property ResetMdt As Boolean = False
        Public Property RestoreKeyboard As Boolean = True
        Public Property SoundAlarm As Boolean = False
        Public Property StartPrinter As Boolean = False

        ''' <summary>
        ''' Default used by ShowScreen: restore keyboard, preserve MDT (do not reset).
        ''' Matches the previous hard-coded &amp;HC2 behaviour.
        ''' </summary>
        Public Shared ReadOnly Property DefaultPreserveMdt As WriteControlCharacter
            Get
                Return New WriteControlCharacter With {
                    .RestoreKeyboard = True,
                    .ResetMdt = False,
                    .SoundAlarm = False,
                    .StartPrinter = False
                }
            End Get
        End Property

        ''' <summary>
        ''' Restore keyboard and clear all MDT bits on the device.
        ''' </summary>
        Public Shared ReadOnly Property ResetMdtAndRestore As WriteControlCharacter
            Get
                Return New WriteControlCharacter With {
                    .RestoreKeyboard = True,
                    .ResetMdt = True,
                    .SoundAlarm = False,
                    .StartPrinter = False
                }
            End Get
        End Property

        ''' <summary>
        ''' Encode to the on-the-wire WCC byte.
        ''' Base pattern 0xC0 keeps the high bits in the range emulators expect;
        ''' semantic flags are OR'd into the low nibble area used by real controllers.
        ''' </summary>
        Public Function ToByte() As Byte
            ' Start from 0xC0 (common "valid WCC" high bits for display writes).
            Dim b As Byte = &HC0
            If StartPrinter Then b = CByte(b Or &H8)
            If SoundAlarm Then b = CByte(b Or &H4)
            If RestoreKeyboard Then b = CByte(b Or &H2)
            If ResetMdt Then b = CByte(b Or &H1)
            Return b
        End Function

        Public Overrides Function ToString() As String
            Return $"WCC=0x{ToByte():X2} (RestoreKB={RestoreKeyboard}, ResetMDT={ResetMdt}, Alarm={SoundAlarm}, Printer={StartPrinter})"
        End Function
    End Class
End Namespace
