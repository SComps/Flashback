Public Class WebAssets
    Public Shared ReadOnly Property Css As String = "
@import url('https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700&display=swap');

* { box-sizing: border-box; }

body {
    font-family: 'IBM Plex Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    background-color: #f4f4f4;
    color: #161616;
    margin: 0;
    padding: 0;
    line-height: 1.5;
}

.container {
    max-width: 1584px;
    margin: 0 auto;
    padding: 0;
}

/* IBM Carbon-inspired Header */
header {
    background-color: #161616;
    color: #ffffff;
    padding: 0;
    margin: 0;
    border-bottom: 1px solid #393939;
    box-shadow: 0 1px 2px rgba(0,0,0,0.1);
}

header .container {
    padding: 16px 32px;
    display: flex;
    align-items: center;
    justify-content: space-between;
}

.header-left {
    display: flex;
    align-items: center;
    gap: 16px;
}

.logo {
    font-size: 1.125rem;
    font-weight: 600;
    color: #ffffff;
    text-decoration: none;
    letter-spacing: 0.5px;
}

.logo:hover {
    color: #78a9ff;
}

h1 {
    margin: 0;
    font-size: 1.125rem;
    font-weight: 600;
    color: #ffffff;
}

.system-info {
    font-size: 0.875rem;
    color: #c6c6c6;
    font-weight: 400;
}

/* Main Content Area */
main {
    padding: 32px;
    min-height: calc(100vh - 120px);
}

.section {
    background-color: #ffffff;
    border: 1px solid #e0e0e0;
    margin-bottom: 24px;
    box-shadow: 0 1px 2px rgba(0,0,0,0.05);
}

.section-header {
    background-color: #f4f4f4;
    border-bottom: 1px solid #e0e0e0;
    padding: 16px 24px;
}

.section-title {
    font-size: 1rem;
    font-weight: 600;
    color: #161616;
    margin: 0;
}

.section-content {
    padding: 0;
}

/* File List - Table Style */
.file-list {
    display: table;
    width: 100%;
    border-collapse: collapse;
}

.file-card {
    display: table-row;
    background-color: #ffffff;
    border-bottom: 1px solid #e0e0e0;
    transition: background-color 0.1s;
}

.file-card:hover {
    background-color: #f4f4f4;
}

.file-card > div {
    display: table-cell;
    padding: 16px 24px;
    vertical-align: middle;
}

.file-info {
    width: 50%;
}

.file-actions {
    width: 50%;
    text-align: right;
    white-space: nowrap;
}

.file-actions form {
    display: inline-block;
    vertical-align: middle;
    margin: 0;
    padding: 0;
}

.file-name {
    font-size: 0.875rem;
    font-weight: 500;
    color: #0f62fe;
    text-decoration: none;
    display: inline-block;
    margin-bottom: 4px;
}

.file-name:hover {
    color: #0043ce;
    text-decoration: underline;
}

.file-name-static {
    font-size: 0.875rem;
    font-weight: 500;
    color: #161616;
    display: inline-block;
    margin-bottom: 4px;
}

.file-meta {
    font-size: 0.75rem;
    color: #525252;
    display: block;
}

/* Buttons */
.btn, a.btn, button.btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    box-sizing: border-box;
    height: 34px;
    padding: 0 16px;
    font-size: 0.875rem;
    font-weight: 600;
    line-height: 1;
    text-align: center;
    text-decoration: none !important;
    border: 1px solid transparent;
    border-radius: 2px;
    cursor: pointer;
    transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease, box-shadow 0.15s ease;
    margin-left: 8px;
    vertical-align: middle;
    font-family: inherit;
    -webkit-appearance: none;
    appearance: none;
}

.btn:focus, a.btn:focus, button.btn:focus {
    outline: 2px solid #0f62fe;
    outline-offset: 2px;
}

/* Primary Button (Download, View Users, View Files, Send Email) */
.btn-primary, a.btn-primary, button.btn-primary {
    background-color: #0f62fe !important;
    color: #ffffff !important;
    border-color: #0f62fe !important;
    text-decoration: none !important;
}

.btn-primary:hover, a.btn-primary:hover, button.btn-primary:hover,
.btn-primary:active, a.btn-primary:active, button.btn-primary:active {
    background-color: #0043ce !important;
    color: #ffffff !important;
    border-color: #0043ce !important;
    box-shadow: 0 2px 4px rgba(0,0,0,0.2) !important;
    text-decoration: none !important;
}

.btn-primary:visited, a.btn-primary:visited {
    color: #ffffff !important;
}

/* Secondary Button (Email, Cancel) */
.btn-secondary, a.btn-secondary, button.btn-secondary {
    background-color: #ffffff !important;
    color: #0f62fe !important;
    border: 1px solid #0f62fe !important;
    text-decoration: none !important;
}

.btn-secondary:hover, a.btn-secondary:hover, button.btn-secondary:hover,
.btn-secondary:active, a.btn-secondary:active, button.btn-secondary:active {
    background-color: #0f62fe !important;
    color: #ffffff !important;
    border-color: #0f62fe !important;
    box-shadow: 0 2px 4px rgba(15,98,254,0.25) !important;
    text-decoration: none !important;
}

.btn-secondary:visited, a.btn-secondary:visited {
    color: #0f62fe !important;
}

.btn-secondary:visited:hover, a.btn-secondary:visited:hover {
    color: #ffffff !important;
}

/* Danger Button (Delete, Restart Engine) */
.btn-danger, a.btn-danger, button.btn-danger {
    background-color: #da1e28 !important;
    color: #ffffff !important;
    border: 1px solid #da1e28 !important;
    text-decoration: none !important;
}

.btn-danger:hover, a.btn-danger:hover, button.btn-danger:hover,
.btn-danger:active, a.btn-danger:active, button.btn-danger:active {
    background-color: #ba1b23 !important;
    color: #ffffff !important;
    border-color: #ba1b23 !important;
    box-shadow: 0 2px 4px rgba(218,30,40,0.3) !important;
    text-decoration: none !important;
}

.btn-danger:visited, a.btn-danger:visited {
    color: #ffffff !important;
}

/* Darker Danger Variant (Stop Engine) */
.btn-danger-dark, a.btn-danger-dark, button.btn-danger-dark {
    background-color: #750e13 !important;
    color: #ffffff !important;
    border: 1px solid #750e13 !important;
    text-decoration: none !important;
}

.btn-danger-dark:hover, a.btn-danger-dark:hover, button.btn-danger-dark:hover,
.btn-danger-dark:active, a.btn-danger-dark:active, button.btn-danger-dark:active {
    background-color: #52070a !important;
    color: #ffffff !important;
    border-color: #52070a !important;
    box-shadow: 0 2px 4px rgba(117,14,19,0.4) !important;
    text-decoration: none !important;
}

.btn-danger-dark:visited, a.btn-danger-dark:visited {
    color: #ffffff !important;
}

/* Warning Button */
.btn-warning, a.btn-warning, button.btn-warning {
    background-color: #d97706 !important;
    color: #ffffff !important;
    border: 1px solid #d97706 !important;
    text-decoration: none !important;
}

.btn-warning:hover, a.btn-warning:hover, button.btn-warning:hover,
.btn-warning:active, a.btn-warning:active, button.btn-warning:active {
    background-color: #b45309 !important;
    color: #ffffff !important;
    border-color: #b45309 !important;
    box-shadow: 0 2px 4px rgba(217,119,6,0.3) !important;
    text-decoration: none !important;
}

.btn-warning:visited, a.btn-warning:visited {
    color: #ffffff !important;
}

/* User Groups */
.user-group {
    margin-bottom: 24px;
}

.user-header {
    background-color: #e0e0e0;
    padding: 12px 24px;
    font-weight: 600;
    font-size: 0.875rem;
    color: #161616;
    border-bottom: 1px solid #c6c6c6;
}

.badge-locked {
    font-size: 0.75rem;
    font-weight: 600;
    color: #da1e28;
    background-color: #fff1f1;
    padding: 2px 8px;
    border-radius: 2px;
    display: inline-block;
    margin-left: 12px;
}

/* Empty State */
.empty-state {
    background-color: #ffffff;
    border: 1px dashed #c6c6c6;
    padding: 48px;
    text-align: center;
    color: #525252;
    font-size: 0.875rem;
    margin: 24px;
}

/* Footer */
.status-bar {
    background-color: #f4f4f4;
    border-top: 1px solid #e0e0e0;
    padding: 12px 32px;
    text-align: left;
    font-size: 0.75rem;
    color: #525252;
}

/* Links (excluding buttons) */
a:not(.btn) {
    color: #0f62fe;
    text-decoration: none;
}

a:not(.btn):hover {
    color: #0043ce;
    text-decoration: underline;
}

a:not(.btn):visited {
    color: #8a3ffc;
}

/* Form Elements */
input[type='text'],
input[type='email'],
textarea {
    width: 100%;
    padding: 12px;
    font-size: 0.875rem;
    font-family: 'IBM Plex Sans', sans-serif;
    border: 1px solid #8d8d8d;
    background-color: #ffffff;
    color: #161616;
    margin-bottom: 16px;
}

input[type='text']:focus,
input[type='email']:focus,
textarea:focus {
    outline: 2px solid #0f62fe;
    outline-offset: -2px;
    border-color: #0f62fe;
}

label {
    display: block;
    font-size: 0.75rem;
    font-weight: 600;
    color: #161616;
    margin-bottom: 8px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

/* Only style bare submit buttons (email form) — .btn class overrides take precedence */
button[type='submit']:not(.btn) {
    background-color: #0f62fe;
    color: #ffffff;
    border: none;
    padding: 12px 24px;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: background-color 0.1s;
}

button[type='submit']:not(.btn):hover {
    background-color: #0043ce;
}

/* Reset browser chrome on button.btn - do NOT reset colour properties */
button.btn {
    appearance: none;
    -webkit-appearance: none;
    border-radius: 0;
    font-family: inherit;
}

/* Admin panel status badges */
.badge-connected {
    display: inline-block;
    padding: 2px 10px;
    font-size: 0.75rem;
    font-weight: 600;
    border-radius: 2px;
    background-color: #defbe6;
    color: #0e6027;
}

.badge-connecting {
    display: inline-block;
    padding: 2px 10px;
    font-size: 0.75rem;
    font-weight: 600;
    border-radius: 2px;
    background-color: #fdf6dd;
    color: #8a4500;
}

.badge-disconnected {
    display: inline-block;
    padding: 2px 10px;
    font-size: 0.75rem;
    font-weight: 600;
    border-radius: 2px;
    background-color: #fff1f1;
    color: #da1e28;
}

.badge-disabled {
    display: inline-block;
    padding: 2px 10px;
    font-size: 0.75rem;
    font-weight: 600;
    border-radius: 2px;
    background-color: #f4f4f4;
    color: #6f6f6f;
}
/* Log tail viewer */
.log-tail {
    display: block;
    width: 100%;
    height: 320px;
    overflow-y: auto;
    overflow-x: auto;
    background-color: #161616;
    color: #f4f4f4;
    font-family: 'IBM Plex Mono', 'Courier New', Courier, monospace;
    font-size: 0.75rem;
    line-height: 1.5;
    padding: 16px;
    margin: 0;
    white-space: pre;
    box-sizing: border-box;
}

.log-line {
    color: #f4f4f4;
}

.log-error {
    color: #ff8389;
}

.log-warn {
    color: #f1c21b;
}

.log-debug {
    color: #6f6f6f;
}

/* Responsive */
@media (max-width: 768px) {
    main { padding: 16px; }
    header .container { padding: 12px 16px; }
    .file-card > div { padding: 12px 16px; }
    .file-info, .file-actions { display: block; width: 100%; }
    .file-actions { text-align: left; margin-top: 8px; white-space: normal; }
    .file-actions .btn, .file-actions form { margin: 4px 6px 4px 0; }
    .log-tail { height: 220px; }
}
"

    Public Shared ReadOnly Property FileIconSvg As String = "<svg xmlns=""http://www.w3.org/2000/svg"" width=""20"" height=""20"" viewBox=""0 0 32 32"" fill=""#0f62fe""><path d=""M25.7 9.3l-7-7A.91.91 0 0018 2H8a2 2 0 00-2 2v24a2 2 0 002 2h16a2 2 0 002-2V10a.91.91 0 00-.3-.7zM18 4.4l5.6 5.6H18zM24 28H8V4h8v6a2 2 0 002 2h6z""/></svg>"
End Class
