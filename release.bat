@ECHO OFF
REM ===========================================================================
REM Copies the output to a release folder structure in ./release
REM ===========================================================================
 
RD /s /q .\release 2> NUL
MD release
IF NOT "%ERRORLEVEL%" == "0" GOTO ERROR

CALL build Release

MD release\client
XCOPY /F src\bin\CSharpTest.Net.BPlusTree.dll release\client
XCOPY /F src\bin\CSharpTest.Net.HttpClone.Hosting.dll release\client
XCOPY /F src\bin\CSharpTest.Net.HttpClone.Library.dll release\client
XCOPY /F src\bin\CSharpTest.Net.Library.dll release\client
XCOPY /F src\bin\CSharpTest.Net.Logging.dll release\client
XCOPY /F src\bin\Google.ProtocolBuffers.dll release\client
XCOPY /F src\bin\Ionic.Zip.Reduced.dll release\client
XCOPY /F src\bin\Lucene.Net.dll release\client
XCOPY /F src\bin\HttpClone.exe release\client
XCOPY /F src\bin\HttpClone.exe.config release\client

MD release\web
XCOPY /F src\SampleWebApp\Web.Release.config release\web
REN release\web\Web.Release.config Web.config
MD release\web\api
XCOPY /F src\SampleWebApp\api\Web.config release\web\api
MD release\web\bin
XCOPY /F src\bin\CSharpTest.Net.BPlusTree.dll release\web\bin
XCOPY /F src\bin\CSharpTest.Net.HttpClone.Hosting.dll release\web\bin
XCOPY /F src\bin\CSharpTest.Net.HttpClone.Library.dll release\web\bin
XCOPY /F src\bin\CSharpTest.Net.Library.dll release\web\bin
XCOPY /F src\bin\CSharpTest.Net.Logging.dll release\web\bin
XCOPY /F src\bin\Google.ProtocolBuffers.dll release\web\bin
XCOPY /F src\bin\Ionic.Zip.Reduced.dll release\web\bin
XCOPY /F src\bin\Lucene.Net.dll release\web\bin



GOTO EXIT

REM ===========================================================================
:ERROR
REM ===========================================================================
ECHO.
ECHO Build Failed.
GOTO ExIT

REM ===========================================================================
:EXIT