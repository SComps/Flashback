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
    width: 60%;
}

.file-actions {
    width: 40%;
    text-align: right;
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

.file-meta {
    font-size: 0.75rem;
    color: #525252;
    display: block;
}

/* Buttons */
.btn {
    display: inline-block;
    padding: 8px 16px;
    font-size: 0.875rem;
    font-weight: 500;
    text-align: center;
    text-decoration: none;
    border: 1px solid transparent;
    cursor: pointer;
    transition: all 0.1s;
    margin-left: 8px;
}

.btn-primary {
    background-color: #0f62fe;
    color: #ffffff;
    border-color: #0f62fe;
}

.btn-primary:hover {
    background-color: #0043ce;
    border-color: #0043ce;
}

.btn-secondary {
    background-color: transparent;
    color: #0f62fe;
    border-color: #0f62fe;
}

.btn-secondary:hover {
    background-color: #e8f4ff;
}

.email-link {
    background-color: transparent;
    color: #0f62fe;
    border: 1px solid #0f62fe;
    padding: 6px 12px;
    font-size: 0.875rem;
    font-weight: 500;
    text-decoration: none;
    display: inline-block;
    transition: all 0.1s;
}

.email-link:hover {
    background-color: #e8f4ff;
    color: #0043ce;
    border-color: #0043ce;
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

/* Links */
a {
    color: #0f62fe;
    text-decoration: none;
}

a:hover {
    color: #0043ce;
    text-decoration: underline;
}

a:visited {
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

button[type='submit'] {
    background-color: #0f62fe;
    color: #ffffff;
    border: none;
    padding: 12px 24px;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: background-color 0.1s;
}

button[type='submit']:hover {
    background-color: #0043ce;
}

/* Responsive */
@media (max-width: 768px) {
    main { padding: 16px; }
    header .container { padding: 12px 16px; }
    .file-card > div { padding: 12px 16px; }
    .file-info, .file-actions { display: block; width: 100%; }
    .file-actions { text-align: left; margin-top: 8px; }
}
"

    Public Shared ReadOnly Property FileIconSvg As String = "<svg xmlns=""http://www.w3.org/2000/svg"" width=""20"" height=""20"" viewBox=""0 0 32 32"" fill=""#0f62fe""><path d=""M25.7 9.3l-7-7A.91.91 0 0018 2H8a2 2 0 00-2 2v24a2 2 0 002 2h16a2 2 0 002-2V10a.91.91 0 00-.3-.7zM18 4.4l5.6 5.6H18zM24 28H8V4h8v6a2 2 0 002 2h6z""/></svg>"
End Class
