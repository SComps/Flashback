Public Class WebAssets
    Public Shared ReadOnly Property Css As String = "
@import url('https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500;600;700&display=swap');
body { font-family: 'IBM Plex Mono', 'Courier New', monospace; background-color: #000080; color: #ffffff; margin: 0; padding: 0; line-height: 1.6; }
.container { max-width: 1400px; margin: 0 auto; padding: 0; }
header { background-color: #000080; color: #00ffff; padding: 0; margin: 0; border-bottom: 2px solid #00ffff; }
header .container { padding: 15px 20px; }
h1 { margin: 0; font-size: 1.3rem; font-weight: 700; color: #00ffff; text-transform: uppercase; letter-spacing: 1px; }
.system-info { font-size: 0.85rem; color: #ffffff; margin-top: 8px; font-weight: 400; }
.section { margin: 0; padding: 20px; background-color: #000080; }
.section-title { font-size: 1.1rem; font-weight: 600; color: #00ffff; margin-bottom: 15px; text-transform: uppercase; letter-spacing: 1px; padding-bottom: 8px; border-bottom: 1px solid #0080ff; }
.file-list { display: block; margin: 0; padding: 0; }
.file-card { background-color: #000080; border: none; border-bottom: 1px solid #0040a0; padding: 12px 20px; text-decoration: none; color: #ffffff; display: flex; align-items: center; justify-content: space-between; transition: background-color 0.15s; position: relative; font-family: 'IBM Plex Mono', monospace; }
.file-card:hover { background-color: #0000a0; }
.file-card a { color: #ffffff; text-decoration: none; }
.file-card a:hover { color: #ffff00; }
.email-link { color: #00ffff !important; font-weight: 600; padding: 4px 12px; border: 1px solid #00ffff; background-color: transparent; transition: all 0.15s; white-space: nowrap; }
.email-link:hover { background-color: #00ffff; color: #000080 !important; }
.thumbnail-container { display: none; }
.file-icon-overlay { display: none; }
.file-icon { display: none; }
.file-icon-container { display: inline; }
.file-name { font-size: 0.95rem; font-weight: 500; color: inherit; display: inline; margin-right: 20px; }
.file-meta { font-size: 0.85rem; color: #00ffff; display: inline; }
.empty-state { background-color: #000080; border: 1px solid #0080ff; padding: 40px; text-align: center; color: #00ffff; font-size: 0.95rem; margin: 20px; }
.user-group { margin: 0; background-color: #000080; border: none; }
.user-header { background-color: #000060; padding: 12px 20px; border-top: 1px solid #0080ff; border-bottom: 1px solid #0080ff; font-weight: 600; font-size: 0.95rem; color: #00ffff; text-transform: uppercase; margin-bottom: 0; }
.badge-locked { font-size: 0.75rem; font-weight: 600; color: #ffff00; background-color: transparent; padding: 0; border: none; display: inline; text-transform: uppercase; position: static; margin-left: 15px; }
a { color: #ffffff; text-decoration: none; }
a:hover { color: #ffff00; }
a:visited { color: #ffffff; }
.status-bar { background-color: #000060; border-top: 2px solid #00ffff; padding: 12px 20px; margin-top: 0; text-align: left; font-size: 0.85rem; color: #00ffff; position: fixed; bottom: 0; left: 0; right: 0; }
main { padding-bottom: 60px; }
"

    Public Shared ReadOnly Property FileIconSvg As String = "<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""currentColor"" class=""file-icon""><path d=""M5.625 1.5c-1.036 0-1.875.84-1.875 1.875v17.25c0 1.035.84 1.875 1.875 1.875h12.75c1.035 0 1.875-.84 1.875-1.875V12.75A3.75 3.75 0 0016.5 9h-1.875a1.875 1.875 0 01-1.875-1.875V5.25A3.75 3.75 0 009 1.5H5.625z"" /><path d=""M12.971 1.816A5.23 5.23 0 0114.25 5.25v1.875c0 .207.168.375.375.375H16.5a5.23 5.23 0 013.434 1.279 9.768 9.768 0 00-6.963-6.963z"" /></svg>"
End Class
