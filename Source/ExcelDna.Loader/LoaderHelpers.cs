﻿/*
  Copyright (C) 2005-2013 Govert van Drimmelen

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.


  Govert van Drimmelen
  govert@icon.co.za
*/

// TODO: Investigate again VSTO / .NET 2.0 security loading problem, 
//       and look at ExecutionContext.SuppressFlow 
//       More info: http://social.msdn.microsoft.com/forums/en-US/clr/thread/0a48607c-5a27-4d12-8e0f-160daed38ef2
//              and http://msdn.microsoft.com/en-us/magazine/cc163644.aspx (Abortable thread pool.)

using System;
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;

namespace ExcelDna.Loader
{
    public static class AppDomainHelper
    {
        // This method is called from unmanaged code in a temporary AppDomain, just to be able to call
        // the right AppDomain.CreateDomain overload.
        public static AppDomain CreateFullTrustSandbox()
        {
            try
            {
                Debug.Print("CreateSandboxAndInitialize - in loader AppDomain with Id: " + AppDomain.CurrentDomain.Id);

                PermissionSet pset = new PermissionSet(PermissionState.Unrestricted);
                AppDomainSetup loaderAppDomainSetup = AppDomain.CurrentDomain.SetupInformation;
                AppDomainSetup sandboxAppDomainSetup = new AppDomainSetup();
                sandboxAppDomainSetup.ApplicationName = loaderAppDomainSetup.ApplicationName;
                sandboxAppDomainSetup.ConfigurationFile = loaderAppDomainSetup.ConfigurationFile;
                sandboxAppDomainSetup.ApplicationBase = loaderAppDomainSetup.ApplicationBase;
                sandboxAppDomainSetup.ShadowCopyFiles = loaderAppDomainSetup.ShadowCopyFiles;
                sandboxAppDomainSetup.ShadowCopyDirectories = loaderAppDomainSetup.ShadowCopyDirectories;

                // create the sandboxed domain
                AppDomain sandbox = AppDomain.CreateDomain(
                    "FullTrustSandbox(" + AppDomain.CurrentDomain.FriendlyName + ")",
                    null,
                    sandboxAppDomainSetup,
                    pset);

                Debug.Print("CreateFullTrustSandbox - sandbox AppDomain created. Id: " + sandbox.Id);

                return sandbox;
            }
            catch (Exception ex)
            {
                Debug.Print("Error during CreateFullTrustSandbox: " + ex.ToString());
                return AppDomain.CurrentDomain;
            }

        }
    }

    internal static class ProcessHelper
    {
        private static bool _isInitialized = false;
        private static bool _isRunningOnCluster;
        private static int _processMajorVersion;

        public static bool IsRunningOnCluster
        {
            get
            {
                EnsureInitialized();
                return _isRunningOnCluster;
            }
        }

        public static int ProcessMajorVersion
        {
            get
            {
                EnsureInitialized();
                return _processMajorVersion;
            }
        }

        public static bool SupportsClusterSafe
        {
            get
            {
                return IsRunningOnCluster || (ProcessMajorVersion >= 14);
            }
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Process hostProcess = Process.GetCurrentProcess();
                _isRunningOnCluster = !(hostProcess.ProcessName.Equals("EXCEL", StringComparison.InvariantCultureIgnoreCase));
                _processMajorVersion = hostProcess.MainModule.FileVersionInfo.FileMajorPart;

                _isInitialized = true;
            }
        }
    }
}
