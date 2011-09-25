===============================================================================
  Welcome to HttpClone -- A website clone, export, and/or publishing utility.
===============================================================================
Why did you build this?

  This tool was built so that I can remove my blog engine from my production
  web server.  I've never really cared for the idea of self-editing websites to
  begin with.  When you add the security issues, install requirements, and the
  performance problems of some blogs together you start looking for another 
  answer.  HttpClone is that answer.  I can now run wordpress locally, take
  a snapshot, and publish it securely over PKI authentication.

What is this for?

  This tool is for anyone looking to make a working clone of a website.  It can
  capture output from an existing server, modify/cleanup the content, and then
  can republish the content via a build-in host or in IIS.

Who is this for?  

  People looking to use this will need a strong working knowledge of http, 
  html/xml, xpath, regular expressions, and probably a little C#.  The tool is
  mostly usable out of the box but requires a lot of configuration.

Why should I care?

  If your like me and want a clean, secure, and fast site this tool can get 
  you there.  The example site used (http://w3example.wordpress.com) can take 
  anywhere from 1 to 3.5 seconds to load with almost nothing on it.  In the
  example, the optimizations reduce the total number of requests from 24 to 7
  and reducing the overall download size from 79k to 9k.  This all makes a
  significant impact on user experience.
  
Getting started:

  The best place to start is to look at the source for example.bat in this 
  directory.  It performs a basic walk-though of the capabilities.  From the 
  initial capturing of a website to clean-up and republication.
  
  The next place to start looking is the configuration file.  Currently the
  example.bat relies on the configuration found at "/src/HttpClone/app.config".
  This configuration file has loads of comments that should help as well as
  an accompanying XSD file for validation.
  
  Once you get a feel for what it's doing and how you can review the detailed
  command-line reference at "/HttpClone-Help.html".

===============================================================================
Revision History
===============================================================================
0.11.924.3

  Alpha-release of the source code.

===============================================================================
