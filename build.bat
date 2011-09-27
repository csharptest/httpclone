@ECHO OFF
REM ===========================================================================
REM Generates the source from protobuffers and builds the application
REM ===========================================================================
@REM 	Must be run from the current directory
@CD /d %~dp0

IF NOT EXIST src\keyfile\httpclone.snk GOTO NOKEY
IF NOT EXIST src\bin\* @MD src\bin 2> NUL

SET BuildConfig=%1
IF "%BuildConfig%" == "" SET BuildConfig=Debug

src\Depends\CmdTool.exe build src\*.csproj
IF NOT "%ERRORLEVEL%" == "0" GOTO ERROR
C:\Windows\Microsoft.NET\Framework\v3.5\MSBuild.exe /nologo /v:m /target:Rebuild /p:Configuration=%BuildConfig% "/p:Platform=Any CPU" /toolsversion:3.5 /l:FileLogger,Microsoft.Build.Engine;logfile=src\bin\MSBuild.log;append=true;verbosity=diagnostic;encoding=utf-8 src\HttpClone.sln
IF NOT "%ERRORLEVEL%" == "0" GOTO ERROR

REM # The following must be deployed with a web project
MD src\SampleWebApp\bin 2> NUL
COPY /Y src\bin\CSharpTest.Net.BPlusTree.dll src\SampleWebApp\bin > NUL
COPY /Y src\bin\CSharpTest.Net.HttpClone.Hosting.dll src\SampleWebApp\bin > NUL
COPY /Y src\bin\CSharpTest.Net.HttpClone.Library.dll src\SampleWebApp\bin > NUL
COPY /Y src\bin\CSharpTest.Net.Library.dll src\SampleWebApp\bin > NUL
COPY /Y src\bin\CSharpTest.Net.Logging.dll src\SampleWebApp\bin > NUL
COPY /Y src\bin\Google.ProtocolBuffers.dll src\SampleWebApp\bin > NUL
COPY /Y src\bin\Ionic.Zip.Reduced.dll src\SampleWebApp\bin > NUL
COPY /Y src\bin\Lucene.Net.dll src\SampleWebApp\bin > NUL

SET BuildConfig=
GOTO EXIT

REM ===========================================================================
:ERROR
REM ===========================================================================
ECHO.
ECHO Build Failed.
SET BuildConfig=
GOTO EXIT

REM ===========================================================================
:NOKEY
REM ===========================================================================
MD src\keyfile
ECHO.
ECHO You must create your own .snk key file before continuing run the following 
ECHO command from a visual studio command-prompt:
ECHO     sn.exe -k %~dp0src\keyfile\httpclone.snk
ECHO.

REM ===========================================================================
:EXIT