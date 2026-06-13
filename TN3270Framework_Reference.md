# TN3270Framework Programmer's Reference Manual

**Version:** 1.0  
**Target Frameworks:** .NET 10.0, .NET 10.0-windows  
**Languages:** Visual Basic .NET, C#  
**Last Updated:** June 2026

---

## Table of Contents

1. [Introduction](#introduction)
2. [Getting Started](#getting-started)
3. [Core Classes](#core-classes)
4. [Events](#events)
5. [3270 Protocol Constants](#3270-protocol-constants)
6. [Field Management](#field-management)
7. [Screen Operations](#screen-operations)
8. [International Character Sets](#international-character-sets)
9. [Code Examples](#code-examples)
10. [Best Practices](#best-practices)
11. [Troubleshooting](#troubleshooting)

---

## Introduction

The **TN3270Framework** is a complete implementation of the IBM 3270 terminal protocol for .NET applications. It provides full support for:

- TN3270 and TN3270E protocols
- Extended attributes (colors, highlighting, intensity)
- International EBCDIC character sets
- Dynamic screen sizing (24x80, 27x132, 32x80, 43x80)
- Complete AID key support (PF1-PF24, PA1-PA3, ENTER, CLEAR)
- Efficient data transfer with READ_MODIFIED commands
- Password field masking (Hidden intensity)

### Key Features

- **Event-Driven Architecture**: React to user input, screen updates, and connection events
- **High-Level API**: Simple methods for screen management and field operations
- **Protocol Compliance**: Full 3270/3278 command and order support
- **Performance Optimized**: Modified Data Tag (MDT) management for efficient data transfer
- **Cross-Platform**: Works on Windows, Linux, and macOS with .NET 10

---

## Getting Started

### Installation

Add reference to `TN3270Framework.dll` in your project.

### Basic Server Setup (VB.NET)

```vb
Imports TN3270Framework

' Create listener on port 3270
Dim listener As New TN3270Listener(3270)

' Handle new connections
AddHandler listener.ConnectionReceived, AddressOf OnConnection

' Start listening
listener.Start()

Private Sub OnConnection(sender As Object, e As TN3270ConnectionEventArgs)
    Dim session = e.Session
    
    ' Handle negotiation complete
    AddHandler session.NegotiationComplete, Sub()
        ' Session is ready - show initial screen
        ShowWelcomeScreen(session)
    End Sub
    
    ' Handle user input
    AddHandler session.AidKeyReceived, AddressOf OnAidKey
    
    ' Start TN3270 negotiation
    session.StartNegotiation()
End Sub
```

### Basic Server Setup (C#)

```csharp
using TN3270Framework;

// Create listener on port 3270
var listener = new TN3270Listener(3270);

// Handle new connections
listener.ConnectionReceived += OnConnection;

// Start listening
listener.Start();

private void OnConnection(object sender, TN3270ConnectionEventArgs e)
{
    var session = e.Session;
    
    // Handle negotiation complete
    session.NegotiationComplete += (s, args) =>
    {
        // Session is ready - show initial screen
        ShowWelcomeScreen(session);
    };
    
    // Handle user input
    session.AidKeyReceived += OnAidKey;
    
    // Start TN3270 negotiation
    session.StartNegotiation();
}
```

---

## Core Classes

### TN3270Listener

Listens for incoming TN3270 connections on a specified port.

#### Constructor

```vb
Public Sub New(port As Integer)
```

**Parameters:**
- `port` - TCP port number to listen on (typically 3270 or 23)

#### Methods

| Method | Description |
|--------|-------------|
| `Start()` | Starts listening for connections |
| `StopListening()` | Stops the listener |

#### Events

| Event | Description |
|-------|-------------|
| `ConnectionReceived(sender, TN3270ConnectionEventArgs)` | Raised when a new client connects |

---

### TN3270Session

Represents an active TN3270 session with a connected client.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Client` | TcpClient | Underlying TCP client (read-only) |
| `Ebcdic` | Encoding | EBCDIC encoding for character conversion |
| `IsNegotiated` | Boolean | True when TN3270 negotiation is complete (read-only) |
| `TerminalType` | String | Client terminal type (e.g., "IBM-3278-2") |
| `Rows` | Integer | Screen height (24, 27, 32, or 43) |
| `Columns` | Integer | Screen width (80 or 132) |
| `ScreenSize` | Integer | Total screen cells (Rows × Columns) (read-only) |
| `Fields` | List(Of TN3270Field) | Collection of screen fields |

#### Methods - Connection Management

```vb
Public Sub StartNegotiation()
```
Initiates TN3270 protocol negotiation with the client.

```vb
Public Sub Close()
```
Closes the session and disconnects the client.

#### Methods - Screen Operations

```vb
Public Sub ClearFields()
```
Removes all fields from the screen.

```vb
Public Function AddField(row As Integer, col As Integer, length As Integer, 
    Optional content As String = "", 
    Optional isProtected As Boolean = True,
    Optional foreground As Byte = TN3270Color.White,
    Optional background As Byte = TN3270Color.Neutral,
    Optional highlighting As Byte = TN3270Highlight.None,
    Optional name As String = "",
    Optional intensity As TN3270Intensity = TN3270Intensity.Normal) As TN3270Field
```
Adds a field to the screen.

**Parameters:**
- `row` - Row position (1-based)
- `col` - Column position (1-based)
- `length` - Field length in characters
- `content` - Initial field content
- `isProtected` - True for protected (display-only) fields
- `foreground` - Foreground color (see TN3270Color)
- `background` - Background color
- `highlighting` - Highlighting style (see TN3270Highlight)
- `name` - Field identifier for retrieval
- `intensity` - Display intensity (Normal, High, Hidden)

**Returns:** The created TN3270Field object

```vb
Public Sub WriteText(row As Integer, col As Integer, text As String,
    Optional foreground As Byte = TN3270Color.White,
    Optional background As Byte = TN3270Color.Neutral)
```
Writes static text to the screen (creates a protected field).

```vb
Public Sub ShowScreen(Optional clearScreen As Boolean = True)
```
Sends the screen to the client. Set `clearScreen` to False for partial updates.

#### Methods - Field Retrieval

```vb
Public Function GetFieldByName(name As String) As TN3270Field
```
Retrieves a field by its name.

```vb
Public Function GetFieldValue(name As String) As String
```
Gets the content of a named field.

```vb
Public Function GetFieldAt(row As Integer, col As Integer) As TN3270Field
```
Finds the field at a specific screen position.

```vb
Public Function GetModifiedFields() As List(Of TN3270Field)
```
Returns all fields with the Modified Data Tag (MDT) set.

```vb
Public Function GetNextUnprotectedField(currentField As TN3270Field) As TN3270Field
```
Gets the next unprotected field (for tab navigation).

#### Methods - Data Transfer Commands

```vb
Public Sub ReadModified()
```
Requests only modified fields from the terminal (most efficient).

```vb
Public Sub ReadModifiedAll()
```
Reads all modified fields including those with MDT off.

```vb
Public Sub ReadBuffer()
```
Reads the entire screen buffer.

```vb
Public Sub EraseAllUnprotected()
```
Clears all unprotected fields on the screen.

```vb
Public Sub ClearModifiedTags()
```
Resets the Modified Data Tag on all fields.

#### Methods - Character Set Management

```vb
Public Sub SetCharacterSet(codePage As String)
```
Changes the EBCDIC encoding for the session.

**Supported Code Pages:**
- `"IBM037"` or `"US"` - US/Canada (default)
- `"IBM273"` or `"GERMAN"` - German/Austrian
- `"IBM277"` or `"DANISH"` - Danish/Norwegian
- `"IBM278"` or `"FINNISH"` - Finnish/Swedish
- `"IBM280"` or `"ITALIAN"` - Italian
- `"IBM284"` or `"SPANISH"` - Spanish
- `"IBM285"` or `"UK"` - UK
- `"IBM297"` or `"FRENCH"` - French
- `"IBM500"` or `"INTERNATIONAL"` - International

#### Events

| Event | Description |
|-------|-------------|
| `NegotiationComplete` | Raised when TN3270 negotiation finishes |
| `AidKeyReceived(sender, AidKeyEventArgs)` | Raised when user presses an AID key |
| `ScreenUpdated` | Raised when screen data is received |
| `DataReceived(sender, DataReceivedEventArgs)` | Raised for raw data packets |
| `Disconnected` | Raised when client disconnects |
| `StructuredFieldReceived(sender, StructuredFieldEventArgs)` | Raised for structured field data |

---

### TN3270Field

Represents a field on the 3270 screen.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | String | Field identifier |
| `Row` | Integer | Row position (1-based) |
| `Col` | Integer | Column position (1-based) |
| `Length` | Integer | Field length in characters |
| `Content` | String | Field content |
| `Address` | Integer | Physical screen address (read-only) |
| `IsProtected` | Boolean | True for display-only fields |
| `IsNumeric` | Boolean | True for numeric-only fields |
| `Modified` | Boolean | Modified Data Tag (MDT) status |
| `Intensity` | TN3270Intensity | Display intensity (Normal, High, Hidden) |
| `ForegroundColor` | Byte | Foreground color |
| `BackgroundColor` | Byte | Background color |
| `Highlighting` | Byte | Highlighting style |
| `Transparency` | Byte | Transparency setting |

#### Methods

```vb
Public Function GetAttributeByte() As Byte
```
Returns the 3270 attribute byte for this field.

---

## Events

### AidKeyEventArgs

Provides data for the `AidKeyReceived` event.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `AidKey` | Byte | The AID key code pressed |
| `ActiveField` | TN3270Field | The field where the cursor was located |
| `CursorAddress` | Integer | Physical cursor position |

#### Example (VB.NET)

```vb
Private Sub OnAidKey(sender As Object, e As AidKeyEventArgs)
    Select Case e.AidKey
        Case AID_KEYS.ENTER
            ProcessEnter(e.ActiveField)
        Case AID_KEYS.PF3
            ExitApplication()
        Case AID_KEYS.PF7
            ScrollUp()
        Case AID_KEYS.PF8
            ScrollDown()
    End Select
End Sub
```

#### Example (C#)

```csharp
private void OnAidKey(object sender, AidKeyEventArgs e)
{
    switch (e.AidKey)
    {
        case AID_KEYS.ENTER:
            ProcessEnter(e.ActiveField);
            break;
        case AID_KEYS.PF3:
            ExitApplication();
            break;
        case AID_KEYS.PF7:
            ScrollUp();
            break;
        case AID_KEYS.PF8:
            ScrollDown();
            break;
    }
}
```

---

## 3270 Protocol Constants

### INTERFACE_CMD

3270 command codes.

| Constant | Value | Description |
|----------|-------|-------------|
| `WRITE` | 0xF1 | Write data to screen |
| `ERASE_WRITE` | 0xF5 | Clear screen and write |
| `ERASE_WRITE_ALTERNATE` | 0x7E | Clear alternate screen (large models) |
| `READ_BUFFER` | 0xF2 | Read entire buffer |
| `READ_MODIFIED` | 0xF6 | Read only modified fields |
| `READ_MODIFIED_ALL` | 0x6E | Read all modified fields |
| `ERASE_ALL_UNPROTECTED` | 0x6F | Clear unprotected fields |
| `WSF` | 0xF3 | Write Structured Field |
| `NOP` | 0xF0 | No Operation |

### ORDER

3270 order codes.

| Constant | Value | Description |
|----------|-------|-------------|
| `SBA` | 0x11 | Set Buffer Address |
| `SF` | 0x1D | Start Field |
| `SFE` | 0x29 | Start Field Extended |
| `IC` | 0x13 | Insert Cursor |
| `SA` | 0x28 | Set Attribute |
| `PT` | 0x05 | Program Tab |
| `RA` | 0x3C | Repeat to Address |
| `EUA` | 0x12 | Erase Unprotected to Address |
| `GE` | 0x08 | Graphic Escape |
| `MF` | 0x2C | Modify Field |

### AID_KEYS

Attention Identifier key codes.

| Constant | Value | Description |
|----------|-------|-------------|
| `ENTER` | 0x7D | Enter key |
| `CLEAR` | 0x6D | Clear key |
| `PA1` | 0x6C | Program Attention 1 |
| `PA2` | 0x6E | Program Attention 2 |
| `PA3` | 0x6B | Program Attention 3 |
| `PF1` - `PF24` | Various | Program Function keys |
| `SYSREQ` | 0xF0 | System Request |
| `STRUCTURED_FIELD` | 0x88 | Structured Field indicator |

**Helper Method:**
```vb
Public Shared Function GetKeyName(aid As Byte) As String
```
Returns the name of an AID key (e.g., "PF3", "ENTER").

### TN3270Color

Color attribute values.

| Constant | Value | Description |
|----------|-------|-------------|
| `Neutral` | 0x0 | Default/no color |
| `Blue` | 0xF1 | Blue |
| `Red` | 0xF2 | Red |
| `Pink` | 0xF3 | Pink/Magenta |
| `Green` | 0xF4 | Green |
| `Turquoise` | 0xF5 | Turquoise/Cyan |
| `Yellow` | 0xF6 | Yellow |
| `White` | 0xF7 | White |

### TN3270Highlight

Highlighting styles.

| Constant | Value | Description |
|----------|-------|-------------|
| `None` | 0x0 | No highlighting |
| `Blink` | 0xF1 | Blinking text |
| `ReverseVideo` | 0xF2 | Reverse video |
| `Underline` | 0xF4 | Underlined text |

### TN3270Intensity

Display intensity levels.

| Value | Description |
|-------|-------------|
| `Normal` | Standard intensity |
| `High` | Bright/bold text |
| `Hidden` | Hidden text (for passwords) |

### TN3270Transparency

Transparency settings.

| Constant | Value | Description |
|----------|-------|-------------|
| `None` | 0x0 | No transparency |
| `Transparent` | 0xF0 | Transparent background |
| `Opaque` | 0xF1 | Opaque background |

---

## Field Management

### Creating Fields

#### Protected Field (Display Only)

```vb
' VB.NET
session.AddField(5, 10, 20, "Hello World", True, TN3270Color.Green)
```

```csharp
// C#
session.AddField(5, 10, 20, "Hello World", true, TN3270Color.Green);
```

#### Unprotected Field (User Input)

```vb
' VB.NET
session.AddField(10, 10, 30, "", False, TN3270Color.White, 
    TN3270Color.Neutral, TN3270Highlight.Underline, "txtUsername")
```

```csharp
// C#
session.AddField(10, 10, 30, "", false, TN3270Color.White,
    TN3270Color.Neutral, TN3270Highlight.Underline, "txtUsername");
```

#### Password Field (Hidden)

```vb
' VB.NET
session.AddField(12, 10, 20, "", False, TN3270Color.Neutral,
    TN3270Color.Neutral, TN3270Highlight.None, "txtPassword", 
    TN3270Intensity.Hidden)
```

```csharp
// C#
session.AddField(12, 10, 20, "", false, TN3270Color.Neutral,
    TN3270Color.Neutral, TN3270Highlight.None, "txtPassword",
    TN3270Intensity.Hidden);
```

### Retrieving Field Values

```vb
' VB.NET
Dim username = session.GetFieldValue("txtUsername")
Dim password = session.GetFieldValue("txtPassword")
```

```csharp
// C#
var username = session.GetFieldValue("txtUsername");
var password = session.GetFieldValue("txtPassword");
```

### Efficient Data Collection

Use `GetModifiedFields()` to only process changed fields:

```vb
' VB.NET
Dim modifiedFields = session.GetModifiedFields()
For Each field In modifiedFields
    Select Case field.Name
        Case "txtName"
            customer.Name = field.Content
        Case "txtEmail"
            customer.Email = field.Content
    End Select
Next
session.ClearModifiedTags() ' Reset after processing
```

```csharp
// C#
var modifiedFields = session.GetModifiedFields();
foreach (var field in modifiedFields)
{
    switch (field.Name)
    {
        case "txtName":
            customer.Name = field.Content;
            break;
        case "txtEmail":
            customer.Email = field.Content;
            break;
    }
}
session.ClearModifiedTags(); // Reset after processing
```

---

## Screen Operations

### Basic Screen Layout

```vb
' VB.NET
Sub ShowMainMenu(session As TN3270Session)
    session.ClearFields()
    
    ' Title
    session.WriteText(1, 25, "MAIN MENU", TN3270Color.White)
    session.WriteText(2, 1, New String("-"c, 80), TN3270Color.Blue)
    
    ' Menu options
    session.WriteText(5, 10, "1. Customer Management", TN3270Color.Turquoise)
    session.WriteText(6, 10, "2. Order Processing", TN3270Color.Turquoise)
    session.WriteText(7, 10, "3. Reports", TN3270Color.Turquoise)
    session.WriteText(8, 10, "4. Exit", TN3270Color.Turquoise)
    
    ' Input field
    session.WriteText(10, 10, "Selection:", TN3270Color.Yellow)
    session.AddField(10, 22, 2, "", False, TN3270Color.White,
        TN3270Color.Neutral, TN3270Highlight.Underline, "txtSelection")
    
    ' Function key help
    session.WriteText(23, 2, "ENTER:Select  PF3:Exit", TN3270Color.White)
    
    session.ShowScreen()
End Sub
```

```csharp
// C#
void ShowMainMenu(TN3270Session session)
{
    session.ClearFields();
    
    // Title
    session.WriteText(1, 25, "MAIN MENU", TN3270Color.White);
    session.WriteText(2, 1, new string('-', 80), TN3270Color.Blue);
    
    // Menu options
    session.WriteText(5, 10, "1. Customer Management", TN3270Color.Turquoise);
    session.WriteText(6, 10, "2. Order Processing", TN3270Color.Turquoise);
    session.WriteText(7, 10, "3. Reports", TN3270Color.Turquoise);
    session.WriteText(8, 10, "4. Exit", TN3270Color.Turquoise);
    
    // Input field
    session.WriteText(10, 10, "Selection:", TN3270Color.Yellow);
    session.AddField(10, 22, 2, "", false, TN3270Color.White,
        TN3270Color.Neutral, TN3270Highlight.Underline, "txtSelection");
    
    // Function key help
    session.WriteText(23, 2, "ENTER:Select  PF3:Exit", TN3270Color.White);
    
    session.ShowScreen();
}
```

### Partial Screen Update

For better performance, update only changed areas:

```vb
' VB.NET
' Update status message without clearing entire screen
session.WriteText(22, 2, "Processing... Please wait", TN3270Color.Yellow)
session.ShowScreen(clearScreen:=False) ' Partial update
```

```csharp
// C#
// Update status message without clearing entire screen
session.WriteText(22, 2, "Processing... Please wait", TN3270Color.Yellow);
session.ShowScreen(clearScreen: false); // Partial update
```

---

## International Character Sets

### Changing Character Set

```vb
' VB.NET
' Switch to German character set
session.SetCharacterSet("IBM273")

' Or use language name
session.SetCharacterSet("GERMAN")
```

```csharp
// C#
// Switch to German character set
session.SetCharacterSet("IBM273");

// Or use language name
session.SetCharacterSet("GERMAN");
```

### Supported Character Sets

| Code Page | Language/Region | Aliases |
|-----------|----------------|---------|
| IBM037 | US/Canada | "US" |
| IBM273 | German/Austrian | "GERMAN", "AUSTRIAN" |
| IBM277 | Danish/Norwegian | "DANISH", "NORWEGIAN" |
| IBM278 | Finnish/Swedish | "FINNISH", "SWEDISH" |
| IBM280 | Italian | "ITALIAN" |
| IBM284 | Spanish | "SPANISH" |
| IBM285 | UK | "UK" |
| IBM297 | French | "FRENCH" |
| IBM500 | International | "INTERNATIONAL" |

---

## Code Examples

### Complete Login Screen (VB.NET)

```vb
Imports TN3270Framework

Public Class LoginHandler
    Private _session As TN3270Session
    
    Public Sub New(session As TN3270Session)
        _session = session
        AddHandler _session.AidKeyReceived, AddressOf OnAidKey
    End Sub
    
    Public Sub ShowLoginScreen()
        _session.ClearFields()
        
        ' Header
        _session.WriteText(1, 30, "SYSTEM LOGIN", TN3270Color.White)
        _session.WriteText(2, 1, New String("="c, 80), TN3270Color.Blue)
        
        ' Username
        _session.WriteText(10, 20, "Username:", TN3270Color.Turquoise)
        _session.AddField(10, 31, 20, "", False, TN3270Color.White,
            TN3270Color.Neutral, TN3270Highlight.Underline, "txtUsername")
        
        ' Password
        _session.WriteText(12, 20, "Password:", TN3270Color.Turquoise)
        _session.AddField(12, 31, 20, "", False, TN3270Color.Neutral,
            TN3270Color.Neutral, TN3270Highlight.None, "txtPassword",
            TN3270Intensity.Hidden)
        
        ' Instructions
        _session.WriteText(23, 2, "ENTER:Login  PF3:Exit", TN3270Color.White)
        
        _session.ShowScreen()
    End Sub
    
    Private Sub OnAidKey(sender As Object, e As AidKeyEventArgs)
        Select Case e.AidKey
            Case AID_KEYS.ENTER
                ProcessLogin()
            Case AID_KEYS.PF3
                _session.Close()
        End Select
    End Sub
    
    Private Sub ProcessLogin()
        Dim username = _session.GetFieldValue("txtUsername")?.Trim()
        Dim password = _session.GetFieldValue("txtPassword")?.Trim()
        
        If ValidateCredentials(username, password) Then
            ShowMainMenu()
        Else
            _session.WriteText(15, 25, "Invalid credentials", TN3270Color.Red)
            _session.ShowScreen(clearScreen:=False)
        End If
    End Sub
    
    Private Function ValidateCredentials(username As String, password As String) As Boolean
        ' Your authentication logic here
        Return username = "admin" AndAlso password = "secret"
    End Function
    
    Private Sub ShowMainMenu()
        ' Navigate to main menu
    End Sub
End Class
```

### Complete Login Screen (C#)

```csharp
using TN3270Framework;

public class LoginHandler
{
    private TN3270Session _session;
    
    public LoginHandler(TN3270Session session)
    {
        _session = session;
        _session.AidKeyReceived += OnAidKey;
    }
    
    public void ShowLoginScreen()
    {
        _session.ClearFields();
        
        // Header
        _session.WriteText(1, 30, "SYSTEM LOGIN", TN3270Color.White);
        _session.WriteText(2, 1, new string('=', 80), TN3270Color.Blue);
        
        // Username
        _session.WriteText(10, 20, "Username:", TN3270Color.Turquoise);
        _session.AddField(10, 31, 20, "", false, TN3270Color.White,
            TN3270Color.Neutral, TN3270Highlight.Underline, "txtUsername");
        
        // Password
        _session.WriteText(12, 20, "Password:", TN3270Color.Turquoise);
        _session.AddField(12, 31, 20, "", false, TN3270Color.Neutral,
            TN3270Color.Neutral, TN3270Highlight.None, "txtPassword",
            TN3270Intensity.Hidden);
        
        // Instructions
        _session.WriteText(23, 2, "ENTER:Login  PF3:Exit", TN3270Color.White);
        
        _session.ShowScreen();
    }
    
    private void OnAidKey(object sender, AidKeyEventArgs e)
    {
        switch (e.AidKey)
        {
            case AID_KEYS.ENTER:
                ProcessLogin();
                break;
            case AID_KEYS.PF3:
                _session.Close();
                break;
        }
    }
    
    private void ProcessLogin()
    {
        var username = _session.GetFieldValue("txtUsername")?.Trim();
        var password = _session.GetFieldValue("txtPassword")?.Trim();
        
        if (ValidateCredentials(username, password))
        {
            ShowMainMenu();
        }
        else
        {
            _session.WriteText(15, 25, "Invalid credentials", TN3270Color.Red);
            _session.ShowScreen(clearScreen: false);
        }
    }
    
    private bool ValidateCredentials(string username, string password)
    {
        // Your authentication logic here
        return username == "admin" && password == "secret";
    }
    
    private void ShowMainMenu()
    {
        // Navigate to main menu
    }
}
```

### Data Entry Form with Validation (VB.NET)

```vb
Sub ShowCustomerForm(session As TN3270Session, customer As Customer)
    session.ClearFields()
    
    ' Title
    session.WriteText(1, 30, "CUSTOMER DETAILS", TN3270Color.White)
    
    ' Fields
    session.WriteText(5, 10, "Customer ID:", TN3270Color.Turquoise)
    session.AddField(5, 25, 10, customer.Id, True, TN3270Color.Yellow)
    
    session.WriteText(7, 10, "Name:", TN3270Color.Turquoise)
    session.AddField(7, 25, 40, customer.Name, False, TN3270Color.White,
        TN3270Color.Neutral, TN3270Highlight.Underline, "txtName")
    
    session.WriteText(9, 10, "Email:", TN3270Color.Turquoise)
    session.AddField(9, 25, 50, customer.Email, False, TN3270Color.White,
        TN3270Color.Neutral, TN3270Highlight.Underline, "txtEmail")
    
    session.WriteText(11, 10, "Phone:", TN3270Color.Turquoise)
    session.AddField(11, 25, 15, customer.Phone, False, TN3270Color.White,
        TN3270Color.Neutral, TN3270Highlight.Underline, "txtPhone")
    
    ' Instructions
    session.WriteText(23, 2, "ENTER:Save  PF3:Cancel  PF12:Delete", TN3270Color.White)
    
    session.ShowScreen()
End Sub

Sub ProcessCustomerForm(session As TN3270Session, customer As Customer)
    ' Use GetModifiedFields for efficiency
    Dim modifiedFields = session.GetModifiedFields()
    
    For Each field In modifiedFields
        Select Case field.Name
            Case "txtName"
                customer.Name = field.Content?.Trim()
            Case "txtEmail"
                customer.Email = field.Content?.Trim()
            Case "txtPhone"
                customer.Phone = field.Content?.Trim()
        End Select
    Next
    
    ' Validate
    Dim errors As New List(Of String)
    If String.IsNullOrEmpty(customer.Name) Then
        errors.Add("Name is required")
    End If
    If Not IsValidEmail(customer.Email) Then
        errors.Add("Invalid email address")
    End If
    
    If errors.Count > 0 Then
        ' Show errors
        Dim row = 15
        For Each err In errors
            session.WriteText(row, 10, err, TN3270Color.Red)
            row += 1
        Next
        session.ShowScreen(clearScreen:=False)
    Else
        ' Save and clear MDT
        SaveCustomer(customer)
        session.ClearModifiedTags()
        ShowSuccessMessage(session)
    End If
End Sub
```

### Scrollable List (VB.NET)

```vb
Public Class CustomerList
    Private _session As TN3270Session
    Private _customers As List(Of Customer)
    Private _startIndex As Integer = 0
    Private Const PageSize As Integer = 10
    
    Public Sub ShowList()
        _session.ClearFields()
        
        ' Header
        _session.WriteText(1, 25, "CUSTOMER LIST", TN3270Color.White)
        _session.WriteText(3, 2, "ID", TN3270Color.Turquoise)
        _session.WriteText(3, 8, "NAME", TN3270Color.Turquoise)
        _session.WriteText(3, 35, "EMAIL", TN3270Color.Turquoise)
        _session.WriteText(4, 1, New String("-"c, 80), TN3270Color.Blue)
        
        ' Display page of customers
        Dim row = 5
        Dim endIndex = Math.Min(_startIndex + PageSize, _customers.Count)
        
        For i = _startIndex To endIndex - 1
            Dim c = _customers(i)
            _session.WriteText(row, 2, c.Id, TN3270Color.Yellow)
            _session.WriteText(row, 8, c.Name.PadRight(25).Substring(0, 25), TN3270Color.White)
            _session.WriteText(row, 35, c.Email.PadRight(40).Substring(0, 40), TN3270Color.White)
            row += 1
        Next
        
        ' Page info
        Dim pageInfo = $"Page {(_startIndex \ PageSize) + 1} of {Math.Ceiling(_customers.Count / PageSize)}"
        _session.WriteText(22, 30, pageInfo, TN3270Color.Turquoise)
        
        ' Instructions
        _session.WriteText(23, 2, "PF7:Up  PF8:Down  PF3:Exit  ENTER:Select", TN3270Color.White)
        
        _session.ShowScreen()
    End Sub
    
    Public Sub HandleInput(e As AidKeyEventArgs)
        Select Case e.AidKey
            Case AID_KEYS.PF7 ' Page Up
                If _startIndex >= PageSize Then
                    _startIndex -= PageSize
                    ShowList()
                End If
            Case AID_KEYS.PF8 ' Page Down
                If _startIndex + PageSize < _customers.Count Then
                    _startIndex += PageSize
                    ShowList()
                End If
            Case AID_KEYS.PF3 ' Exit
                _session.Close()
        End Select
    End Sub
End Class
```

---

## Best Practices

### 1. Use Modified Data Tags (MDT) Efficiently

Always use `GetModifiedFields()` instead of retrieving all field values:

```vb
' ❌ BAD - Retrieves all fields
Dim name = session.GetFieldValue("txtName")
Dim email = session.GetFieldValue("txtEmail")
Dim phone = session.GetFieldValue("txtPhone")

' ✅ GOOD - Only processes changed fields
Dim modifiedFields = session.GetModifiedFields()
For Each field In modifiedFields
    ' Process only changed fields
Next
session.ClearModifiedTags() ' Reset after processing
```

### 2. Clear MDT After Processing

Always call `ClearModifiedTags()` after saving data:

```vb
SaveCustomer(customer)
session.ClearModifiedTags() ' Prevents reprocessing unchanged data
```

### 3. Use Partial Updates for Status Messages

Don't clear the entire screen for status updates:

```vb
' ✅ GOOD - Partial update
session.WriteText(22, 2, "Record saved successfully", TN3270Color.Green)
session.ShowScreen(clearScreen:=False)
```

### 4. Handle All Common AID Keys

Always handle ENTER, PF3 (Exit), and relevant function keys:

```vb
Select Case e.AidKey
    Case AID_KEYS.ENTER
        ProcessInput()
    Case AID_KEYS.PF3
        ReturnToPreviousScreen()
    Case AID_KEYS.PF12
        ShowHelp()
    Case Else
        ' Refresh screen for unknown keys
        ShowCurrentScreen()
End Select
```

### 5. Use Named Fields

Always name input fields for easy retrieval:

```vb
' ✅ GOOD
session.AddField(10, 20, 30, "", False, name:="txtUsername")
Dim username = session.GetFieldValue("txtUsername")

' ❌ BAD - Hard to retrieve
session.AddField(10, 20, 30, "", False)
```

### 6. Validate Input

Always validate user input before processing:

```vb
Dim email = session.GetFieldValue("txtEmail")?.Trim()
If String.IsNullOrEmpty(email) OrElse Not IsValidEmail(email) Then
    session.WriteText(15, 10, "Invalid email address", TN3270Color.Red)
    session.ShowScreen(clearScreen:=False)
    Return
End If
```

### 7. Use Appropriate Field Attributes

- **Protected fields** for labels and display-only data
- **Unprotected fields** for user input
- **Hidden intensity** for passwords
- **High intensity** for important information
- **Colors** to distinguish field types

```vb
' Label (protected)
session.WriteText(5, 10, "Username:", TN3270Color.Turquoise)

' Input field (unprotected, underlined)
session.AddField(5, 21, 20, "", False, TN3270Color.White,
    TN3270Color.Neutral, TN3270Highlight.Underline, "txtUsername")

' Password (hidden)
session.AddField(7, 21, 20, "", False, TN3270Color.Neutral,
    TN3270Color.Neutral, TN3270Highlight.None, "txtPassword",
    TN3270Intensity.Hidden)

' Error message (high intensity, red)
session.AddField(10, 10, 50, "ERROR: Invalid input", True,
    TN3270Color.Red, intensity:=TN3270Intensity.High)
```

### 8. Handle Disconnections Gracefully

Always handle the `Disconnected` event:

```vb
AddHandler session.Disconnected, Sub()
    ' Clean up resources
    CleanupSession(session)
    Console.WriteLine($"Client disconnected: {session.Client.Client.RemoteEndPoint}")
End Sub
```

### 9. Use Consistent Screen Layout

Maintain consistent positioning for common elements:

- Row 1: Title/Program ID
- Row 2: Separator line
- Rows 3-21: Content area
- Row 22: Status messages
- Row 23: Function key help

### 10. Test with Different Terminal Sizes

Test your application with various screen sizes:

- 24x80 (standard)
- 27x132 (Model 5)
- 32x80 (Model 3)
- 43x80 (Model 4)

```vb
Console.WriteLine($"Terminal: {session.TerminalType} ({session.Columns}x{session.Rows})")
```

---

## Troubleshooting

### Problem: Fields Not Accepting Input

**Cause:** Field is marked as protected.

**Solution:** Set `isProtected` to `False`:

```vb
session.AddField(10, 20, 30, "", False, name:="txtInput") ' False = unprotected
```

### Problem: Password Field Shows Characters

**Cause:** Intensity not set to Hidden.

**Solution:** Use `TN3270Intensity.Hidden`:

```vb
session.AddField(12, 20, 20, "", False, intensity:=TN3270Intensity.Hidden)
```

### Problem: Colors Not Displaying

**Cause:** Terminal doesn't support extended attributes.

**Solution:** Check terminal type and use basic attributes as fallback:

```vb
If session.TerminalType.Contains("3278") Then
    ' Use colors
    session.AddField(5, 10, 20, "Text", True, TN3270Color.Green)
Else
    ' Use basic attributes only
    session.AddField(5, 10, 20, "Text", True)
End If
```

### Problem: Screen Size Wrong

**Cause:** Using ERASE_WRITE instead of ERASE_WRITE_ALTERNATE for large screens.

**Solution:** The framework automatically handles this in `ShowScreen()`. Ensure you're not manually sending commands.

### Problem: Modified Fields Not Detected

**Cause:** MDT not set or already cleared.

**Solution:** 
1. Don't manually set `.Modified = True` on fields
2. Only call `ClearModifiedTags()` after processing
3. Use `ReadModified()` to request data from terminal

### Problem: International Characters Garbled

**Cause:** Wrong EBCDIC code page.

**Solution:** Set the correct character set:

```vb
session.SetCharacterSet("IBM273") ' For German
```

### Problem: Cursor Not Positioning Correctly

**Cause:** No unprotected fields or IC order not sent.

**Solution:** Ensure at least one unprotected field exists. The framework automatically positions the cursor at the first unprotected field.

### Problem: Session Hangs After ShowScreen()

**Cause:** Missing IAC EOR delimiter.

**Solution:** The framework automatically adds this. If manually sending data, always end with:

```vb
buffer.Add(&HFF) ' IAC
buffer.Add(&HEF) ' EOR
```

---

## Performance Tips

### 1. Use READ_MODIFIED Instead of READ_BUFFER

```vb
' ✅ Efficient - Only reads changed fields
session.ReadModified()

' ❌ Inefficient - Reads entire screen
session.ReadBuffer()
```

### 2. Minimize Screen Refreshes

```vb
' ✅ Update multiple fields, then show once
session.WriteText(10, 10, "Line 1", TN3270Color.White)
session.WriteText(11, 10, "Line 2", TN3270Color.White)
session.WriteText(12, 10, "Line 3", TN3270Color.White)
session.ShowScreen()

' ❌ Multiple refreshes
session.WriteText(10, 10, "Line 1", TN3270Color.White)
session.ShowScreen()
session.WriteText(11, 10, "Line 2", TN3270Color.White)
session.ShowScreen()
```

### 3. Use Partial Updates for Status Messages

```vb
session.WriteText(22, 2, "Processing...", TN3270Color.Yellow)
session.ShowScreen(clearScreen:=False) ' Faster
```

### 4. Process Only Modified Fields

```vb
Dim modifiedFields = session.GetModifiedFields()
' Process only changed fields - much faster for large forms
```

---

## Appendix A: Complete AID Key Reference

| Key | Code | Hex | Description |
|-----|------|-----|-------------|
| ENTER | 125 | 0x7D | Enter/Return key |
| CLEAR | 109 | 0x6D | Clear screen |
| PA1 | 108 | 0x6C | Program Attention 1 |
| PA2 | 110 | 0x6E | Program Attention 2 |
| PA3 | 107 | 0x6B | Program Attention 3 |
| PF1 | 241 | 0xF1 | Program Function 1 |
| PF2 | 242 | 0xF2 | Program Function 2 |
| PF3 | 243 | 0xF3 | Program Function 3 (often Exit) |
| PF4 | 244 | 0xF4 | Program Function 4 |
| PF5 | 245 | 0xF5 | Program Function 5 |
| PF6 | 246 | 0xF6 | Program Function 6 |
| PF7 | 247 | 0xF7 | Program Function 7 (often Page Up) |
| PF8 | 248 | 0xF8 | Program Function 8 (often Page Down) |
| PF9 | 249 | 0xF9 | Program Function 9 |
| PF10 | 122 | 0x7A | Program Function 10 |
| PF11 | 123 | 0x7B | Program Function 11 |
| PF12 | 124 | 0x7C | Program Function 12 (often Help) |
| PF13 | 193 | 0xC1 | Program Function 13 |
| PF14 | 194 | 0xC2 | Program Function 14 |
| PF15 | 195 | 0xC3 | Program Function 15 |
| PF16 | 196 | 0xC4 | Program Function 16 |
| PF17 | 197 | 0xC5 | Program Function 17 |
| PF18 | 198 | 0xC6 | Program Function 18 |
| PF19 | 199 | 0xC7 | Program Function 19 |
| PF20 | 200 | 0xC8 | Program Function 20 |
| PF21 | 201 | 0xC9 | Program Function 21 |
| PF22 | 74 | 0x4A | Program Function 22 |
| PF23 | 75 | 0x4B | Program Function 23 |
| PF24 | 76 | 0x4C | Program Function 24 |
| SYSREQ | 240 | 0xF0 | System Request |

---

## Appendix B: EBCDIC Code Page Reference

| Code Page | IBM Number | Language/Region | Special Characters |
|-----------|------------|-----------------|-------------------|
| IBM037 | 37 | US/Canada | Standard ASCII mapping |
| IBM273 | 273 | German/Austrian | ä, ö, ü, ß, Ä, Ö, Ü |
| IBM277 | 277 | Danish/Norwegian | æ, ø, å, Æ, Ø, Å |
| IBM278 | 278 | Finnish/Swedish | ä, ö, å, Ä, Ö, Å |
| IBM280 | 280 | Italian | à, è, é, ì, ò, ù |
| IBM284 | 284 | Spanish | á, é, í, ó, ú, ñ, ¿, ¡ |
| IBM285 | 285 | UK | £ (pound sign) |
| IBM297 | 297 | French | à, â, ç, é,