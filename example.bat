@REM 	*****************************************************************************************
@REM 	* Welcome, the following is a rather shady example of how to use the command-line and web
@REM 	* hosting of HttpClone.  It walks through the initial creation and crawling of a site, to
@REM 	* the remote publication and hosting.  The following commands are by no means all it can
@REM 	* do, but they are most common and vital to getting up and running.
@REM 	*****************************************************************************************

@REM 	Must be run from the current directory
@CD /d %~dp0

REM 	Let's start by removing the existing site if one exists
src\bin\httpclone.exe deletesite http://localhost:11080 /noprompt

REM 	The first thing to do here is go to collect our sample website, w3example.wordpress.com, and save
REM 	it (renaming all links) into our test site, localhost:11080
src\bin\httpclone.exe update http://localhost:11080 http://w3example.wordpress.com

REM 	Now that we've imported the w3example.wordpress.com domain, we want to add some files from other
REM 	locations.  The first thing we want to collect is the style-sheets that are on s0.wp.com, we will
REM 	addrecursive here so that all images, stylesheets, etc that they point to are also imported. While
REM 	we are importing, we will rebase them at the root of the site '/global.css' and '/style.css'.
src\bin\httpclone.exe addrecursive http://localhost:11080/global.css http://s0.wp.com/wp-content/themes/h4/global.css
src\bin\httpclone.exe addrecursive http://localhost:11080/style.css http://s0.wp.com/wp-content/themes/pub/titan/style.css

REM 	After importing these styles, we want to map all style references to use these locations instead
REM 	of their original addresses.
src\bin\httpclone.exe relinkmatching http://localhost:11080 ^http://s0\.wp\.com/wp-content/themes/h4/global.css http://localhost:11080/global.css
src\bin\httpclone.exe relinkmatching http://localhost:11080 ^http://s0\.wp\.com/wp-content/themes/pub/titan/style.css http://localhost:11080/style.css

REM 	Before we go any further, let's apply all our content filtering rules we defined for each document
REM 	type in the configuration.  This will help us in the next step to identify duplcate pages.
src\bin\httpclone.exe optimize http://localhost:11080

REM 	Another thing the crawler can have trouble with is recognizing duplicate content.  It's always best
REM 	to remove that duplication entirely, or at least insert redirects.  The following finds duplicates
REM 	and redirects them:
src\bin\httpclone.exe dedupcontent http://localhost:11080 /noprompt

REM 	Now we can run index to produced the search index, the search.css stylesheet, and the template
src\bin\httpclone.exe index http://localhost:11080

REM 	If we want to crawl and examine the html prior to publication on the site, one option is to export
REM 	the site to html.  This option works well enough to perform basic browsing; however, some things
REM 	like http-redirects will not be supported nor corrected.  The following exports the site to your
REM 	temp directory:
@MD %TEMP%\w3example 2> NUL
src\bin\httpclone.exe export http://localhost:11080 %TEMP%\w3example

SET VSPROGFILES=%ProgramFiles(x86)%
IF "%VSPROGFILES%" == "" SET VSPROGFILES=%ProgramFiles%

REM 	Finally, we have two options, first we could run the self-hosting to view the results in the browser...
IF EXIST "%VSPROGFILES%\Common Files\microsoft shared\DevServer\9.0\WebDev.WebServer.EXE" GOTO VSWebDEV
src\bin\httpclone.exe host http://localhost:11080
GOTO FINISH

:VSWebDEV
REM 	Second, we could run it in IIS or in the cassini develpment server (VS2008).  To run in either of
REM 	these we will need to create a set of keys so that we can publish data to the site, and then we will
REM 	need to publish data to the site.  For those with VS2008 installed, this is the process:
IF EXIST "src\bin\Store\localhost.11080\client-publishing.key" GOTO HAVEKEY
REM 	First we create the keypair for publishing (for production do not use /noserverpassword)
src\bin\httpclone.exe createkeys http://localhost:11080 /noserverpassword
REM 	Now MOVE, not copy, the file to the web server's bin directory
MOVE src\bin\Store\localhost.11080\server-publishing.key src\SampleWebApp\bin

:HAVEKEY
REM 	Start the web server in our sample path and on the port we intend to use
start "WebServer" "%VSPROGFILES%\Common Files\microsoft shared\DevServer\9.0\WebDev.WebServer.EXE" /port:11080 "/path:%~dp0src\SampleWebApp"
@REM 	The server takes a little while to start up, this delays us a second or so
@FOR /L %%i IN (1,1,1000) DO @ECHO %%i > NUL

REM 	Now publish the content to the webserver
src\bin\httpclone.exe publish http://localhost:11080
REM 	Ready for browsing immediatly after this returns
IF "%ERRORLEVEL%" == "0" start http://localhost:11080

:FINISH
