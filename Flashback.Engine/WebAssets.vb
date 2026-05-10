Namespace Flashback.Engine
    Public Class WebAssets
        Public Shared ReadOnly Property Css As String = "
body { font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; background-color: #f3f4f6; color: #1f2937; margin: 0; padding: 0; }
.container { max-width: 1000px; margin: 0 auto; padding: 20px; }
header { background-color: #fff; border-bottom: 1px solid #e5e7eb; padding: 20px 0; margin-bottom: 30px; box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05); }
h1 { margin: 0; font-size: 1.5rem; font-weight: 700; color: #111827; }
.section { margin-bottom: 40px; }
.section-title { font-size: 1.125rem; font-weight: 600; color: #374151; margin-bottom: 15px; display: flex; align-items: center; }
.section-title svg { width: 20px; height: 20px; margin-right: 8px; color: #6b7280; }
.file-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 15px; }
.file-card { background-color: #fff; border: 1px solid #e5e7eb; border-radius: 8px; padding: 15px; text-decoration: none; color: inherit; display: flex; flex-direction: column; align-items: center; transition: all 0.2s; position: relative; }
.file-card:hover { border-color: #3b82f6; box-shadow: 0 4px 12px -2px rgba(0, 0, 0, 0.15); transform: translateY(-4px); }
.thumbnail-container { width: 100%; height: 140px; background-color: #f3f4f6; border-radius: 4px; margin-bottom: 12px; overflow: hidden; position: relative; border: 1px solid #e5e7eb; }
.pdf-preview { width: 400%; height: 400%; transform: scale(0.25); transform-origin: 0 0; pointer-events: none; border: none; }
.file-icon-overlay { position: absolute; top: 8px; right: 8px; width: 24px; height: 24px; color: #3b82f6; opacity: 0.8; }
.file-icon { width: 40px; height: 40px; color: #3b82f6; margin-bottom: 10px; }
.file-name { font-size: 0.875rem; font-weight: 500; text-align: center; word-break: break-all; margin-bottom: 4px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
.file-meta { font-size: 0.75rem; color: #6b7280; }
.empty-state { background-color: #fff; border: 2px dashed #e5e7eb; border-radius: 8px; padding: 40px; text-align: center; color: #9ca3af; font-size: 0.875rem; }
.user-group { margin-bottom: 30px; background-color: #fff; border: 1px solid #e5e7eb; border-radius: 8px; overflow: hidden; }
.user-header { background-color: #f9fafb; padding: 12px 20px; border-bottom: 1px solid #e5e7eb; font-weight: 600; font-size: 0.875rem; color: #4b5563; display: flex; justify-content: space-between; align-items: center; }
.badge-locked { font-size: 0.75rem; font-weight: 500; color: #059669; background-color: #ecfdf5; padding: 2px 8px; border-radius: 9999px; display: flex; align-items: center; gap: 4px; }
"

        Public Shared ReadOnly Property FileIconSvg As String = "<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""currentColor"" class=""file-icon""><path d=""M5.625 1.5c-1.036 0-1.875.84-1.875 1.875v17.25c0 1.035.84 1.875 1.875 1.875h12.75c1.035 0 1.875-.84 1.875-1.875V12.75A3.75 3.75 0 0016.5 9h-1.875a1.875 1.875 0 01-1.875-1.875V5.25A3.75 3.75 0 009 1.5H5.625z"" /><path d=""M12.971 1.816A5.23 5.23 0 0114.25 5.25v1.875c0 .207.168.375.375.375H16.5a5.23 5.23 0 013.434 1.279 9.768 9.768 0 00-6.963-6.963z"" /></svg>"
    End Class
End Namespace
