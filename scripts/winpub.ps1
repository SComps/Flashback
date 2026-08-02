param(
    [string]$PublishDir = "E:\Flashback-Publish"
)

& "$PSScriptRoot\publish_windows.ps1" -PublishDir $PublishDir
& "$PSScriptRoot\publish_wpf.ps1"     -PublishDir $PublishDir
& "$PSScriptRoot\publish_winui.ps1"   -PublishDir $PublishDir
