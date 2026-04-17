Imports System
Imports System.Net.Sockets
Imports System.Text
Imports System.IO

Module Program
    Sub Main(args As String())
        ' Check for help anywhere in args
        For Each arg In args
            If arg.ToLower() = "-h" OrElse arg.ToLower() = "--help" Then
                ShowHelp()
                Environment.Exit(0)
            End If
        Next

        If args.Length > 2 Then
            System.Console.WriteLine("Too many arguments.")
            ShowHelp()
            Environment.Exit(1)
        End If

        For Each arg In args
            If arg.StartsWith("-") Then
                System.Console.WriteLine($"Unknown option: {arg}")
                ShowHelp()
                Environment.Exit(1)
            End If
        Next

        Dim host As String = "127.0.0.1"
        Dim port As Integer = 9100

        If args.Length >= 1 Then host = args(0)
        If args.Length >= 2 Then port = Val(args(1))

        Console.ForegroundColor = ConsoleColor.Cyan
        Console.WriteLine("==================================================")
        Console.WriteLine(" FLASHBACK PORT 9100 TEST TOOL")
        Console.WriteLine("==================================================")
        Console.ResetColor()
        Console.WriteLine($"Target: {host}:{port}")

        Try
            Console.WriteLine("Connecting...")
            Using client As New TcpClient(host, port)
                Using stream = client.GetStream()
                    Console.WriteLine("Connected! Sending test data...")

                    Dim sb As New StringBuilder()
                    
                    ' Page 1
                    sb.AppendLine("FLASHBACK TEST JOB - PAGE 1")
                    sb.AppendLine(New String("-"c, 40))
                    sb.AppendLine("This is a test of the Port 9100 listener.")
                    sb.AppendLine("Data sent from this tool should appear in a PDF")
                    sb.AppendLine("using the GENERIC OS profile.")
                    sb.AppendLine()
                    sb.AppendLine("Line with some overstrike:")
                    sb.Append("UNDERLINED TEXT")
                    sb.Append(ControlChars.Cr)
                    sb.AppendLine(New String("_"c, 15))
                    sb.AppendLine()
                    sb.AppendLine("End of Page 1.")
                    sb.Append(ControlChars.FormFeed)

                    ' Page 2
                    sb.AppendLine("FLASHBACK TEST JOB - PAGE 2")
                    sb.AppendLine(New String("="c, 40))
                    sb.AppendLine("This is the second page of the test.")
                    sb.AppendLine("Generated on: " & DateTime.Now.ToString())
                    sb.AppendLine()
                    sb.AppendLine("Testing long line wrapping behavior (if enabled):")
                    sb.AppendLine("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.")
                    sb.AppendLine()
                    sb.AppendLine("Closing connection to trigger PDF generation...")

                    Dim data = Encoding.UTF8.GetBytes(sb.ToString())
                    stream.Write(data, 0, data.Length)
                    
                    Console.ForegroundColor = ConsoleColor.Green
                    Console.WriteLine($"Success! Sent {data.Length} bytes.")
                End Using
            End Using
        Catch ex As Exception
            Console.ForegroundColor = ConsoleColor.Red
            Console.WriteLine($"ERROR: {ex.Message}")
        End Try

        Console.ResetColor()
        Console.WriteLine("Press any key to exit...")
        Console.ReadKey()
    End Sub

    Private Sub ShowHelp()
        Console.WriteLine("Flashback Port 9100 Test Tool")
        Console.WriteLine("Usage: Flashback.TestTool [host] [port]")
        Console.WriteLine()
        Console.WriteLine("Options:")
        Console.WriteLine("  -h, --help            Show this help message")
        Console.WriteLine()
    End Sub
End Module
