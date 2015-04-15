using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Management;
using gitlab_ci_runner.api;
using gitlab_ci_runner.conf;
using Microsoft.Experimental.IO;
using gitlab_ci_runner.helper;

namespace gitlab_ci_runner.runner
{
    class Build
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="buildInfo">Build Info</param>
        public Build(BuildInfo buildInfo)
        {
            this.buildInfo = buildInfo;
            sProjectDir = Path.Combine(sProjectsDir, helper.PathHelper.makeValidPath(buildInfo.project_name));
            commands = new List<Command>();
            outputList = new ConcurrentQueue<string>();
            state = State.WAITING;
        }
      
        /// <summary>
        /// Destructor - Making sure that the process object is killed!
        /// </summary>
        ~Build()  // destructor
        {
            // cleanup statements...
            if (process != null)
            {
                killProcessAndChildren(process.Id);
                process.Close();
                process = null;
            }
        }

        /// <summary>
        /// Kill a process, and all of its children, grandchildren, etc.
        /// Need to add System.Management to References manually to enable this.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        private static void killProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
              ("Select * From Win32_Process Where ParentProcessID=" + pid);
            
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                killProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

        /// <summary>
        /// Command output
        /// Build internal!
        /// </summary>
        private ConcurrentQueue<string> outputList;

        /// <summary>
        /// Process object
        /// Build internal!
        /// </summary>
        private Process process = null;

        /// <summary>
        /// Command output
        /// </summary>
        public string output
        {
            get
            {
                string t;
                while (outputList.TryPeek(out t) && string.IsNullOrEmpty(t))
                {
                    outputList.TryDequeue(out t);
                }
                return String.Join("\n", outputList.ToArray()) + "\n";
            }
        }

        /// <summary>
        /// Projects Directory
        /// </summary>
        private string sProjectsDir = Config.workingdir;

        /// <summary>
        /// Project Directory
        /// </summary>
        private string sProjectDir;

        /// <summary>
        /// Build Infos
        /// </summary>
        public BuildInfo buildInfo;

        /// <summary>
        /// Command list
        /// </summary>
        private List<Command> commands;

        /// <summary>
        /// Execution State
        /// </summary>
        public State state { get; private set; }

        /// <summary>
        /// Command Timeout
        /// </summary>
        public int iTimeout
        {
            get
            {
                return this.buildInfo.timeout;
            }
        }

        /// <summary>
        /// Run the Build Job
        /// </summary>
        public void run()
        {
            state = State.RUNNING;
            
            try {

                // Initialize project dir
                initProjectDir();
    
                // Add build commands
                foreach (string sCommand in buildInfo.GetCommands())
                {
                    // Skip empty lines
                    if (String.IsNullOrEmpty(sCommand.Trim()))
                        continue;

                    commands.Add(new Command(sCommand));
                }

                // Execute
                foreach (Command sCommand in commands)
                {
                    if (!exec(sCommand))
                    {
                        // only change the state if not already aborted 
                        if (state != State.ABORTED)
                            state = State.FAILED;
                        break;
                    }
                }
    
                if (state == State.RUNNING)
                {
                    // All commands executed correctly
                    state = State.SUCCESS;
                }
                
            } catch (Exception rex) {
                outputList.Enqueue("");
                outputList.Enqueue("A runner exception occoured: " + rex.Message);
                outputList.Enqueue("");
                state = State.FAILED;

                return;
            }
        }

        /// <summary>
        /// Terminate the Build Job
        /// </summary>
        public void terminate()
        {
            // Build process
            if (process != null)
            {
                try
                {
                    killProcessAndChildren(process.Id);
                    process.Close();
                    process = null;

                    state = State.ABORTED;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" Exception caught when terminating build process: ", ex.Message);
                    state = State.FAILED;
                    return;
                }
            }
        }

        /// <summary>
        /// Initialize project dir and checkout repo
        /// </summary>
        private void initProjectDir()
        {
            // Check if projects directory exists
            if (!Directory.Exists(sProjectsDir))
            {
                // Create projects directory
                Directory.CreateDirectory(sProjectsDir);
            }

            // Check if already a git repo
            if (Directory.Exists(sProjectDir + @"\.git") && buildInfo.allow_git_fetch)
            {
                // Already a git repo, pull changes
                commands.Add(fetchCmd());
                commands.Add(checkoutCmd());
            }
            else
            {
                // No git repo, checkout
                if (Directory.Exists(sProjectDir))
                    DeleteDirectory(sProjectDir);

                commands.Add(cloneCmd());
                commands.Add(checkoutCmd());
            }
        }

        private bool exec(Command command)
        {
            try
            {
                var sCommand = command.ToString();

                // Remove Whitespaces
                sCommand = sCommand.Trim();

                // Output command
                outputList.Enqueue(sCommand);

                // Build process
                if (process == null)
                    process = new Process();
                else
                {
                    killProcessAndChildren(process.Id);
                    process.Close();
                    process = null;

                    process = new Process();
                }

                process.StartInfo.UseShellExecute = false;
                if (Directory.Exists(sProjectDir))
                {
                    process.StartInfo.WorkingDirectory = sProjectDir; // Set Current Working Directory to project directory
                }
                process.StartInfo.FileName = "cmd.exe"; // use cmd.exe so we dont have to split our command in file name and arguments
                process.StartInfo.Arguments = "/C \"" + sCommand + "\""; // pass full command as arguments

                // Environment variables
                process.StartInfo.EnvironmentVariables["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); // Fix for missing SSH Key

                process.StartInfo.EnvironmentVariables["BUNDLE_GEMFILE"] = sProjectDir + @"\Gemfile";
                process.StartInfo.EnvironmentVariables["BUNDLE_BIN_PATH"] = "";
                process.StartInfo.EnvironmentVariables["RUBYOPT"] = "";

                process.StartInfo.EnvironmentVariables["CI_SERVER"] = "yes";
                process.StartInfo.EnvironmentVariables["CI_SERVER_NAME"] = "GitLab CI";
                process.StartInfo.EnvironmentVariables["CI_SERVER_VERSION"] = null; // GitlabCI Version
                process.StartInfo.EnvironmentVariables["CI_SERVER_REVISION"] = null; // GitlabCI Revision

                process.StartInfo.EnvironmentVariables["CI_BUILD_REF"] = buildInfo.sha;
                process.StartInfo.EnvironmentVariables["CI_BUILD_REF_NAME"] = buildInfo.@ref;
                process.StartInfo.EnvironmentVariables["CI_BUILD_ID"] = buildInfo.id.ToString();
                process.StartInfo.EnvironmentVariables["GIT_SSL_NO_VERIFY"] = "true";

                // Redirect Standard Output and Standard Error
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.OutputDataReceived += new DataReceivedEventHandler(outputHandler);
                process.ErrorDataReceived += new DataReceivedEventHandler(outputHandler);
                //process.Exited += new EventHandler(process_Exited);

                try
                {
                    int exitCode = -1;
                    // Run the command
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Make sure we wait till the process is finished
                    if (process.WaitForExit(iTimeout * 1000))
                    {
                        // https://msdn.microsoft.com/en-us/library/ty0d8k56(v=vs.110).aspx
                        // When standard output has been redirected to asynchronous event handlers, 
                        // it is possible that output processing will not have completed when this method returns. 
                        // To ensure that asynchronous event handling has been completed, call the WaitForExit() 
                        // overload that takes no parameter after receiving a true from the WaitForExit(Int32) overload. 
                        process.WaitForExit();

                        if (process.HasExited)
                        {
                            // Record the exit code
                            exitCode = process.ExitCode;
                        }
                        else
                        {
                            Console.WriteLine("[" + DateTime.Now.ToString() + "] Process " + process.Id + " hasn't exited properly. Exit code might be invalid.");
                        }
                    }
                    // Terminate process
                    killProcessAndChildren(process.Id);
                    return (exitCode == 0);
                }
                finally
                {
                    process.OutputDataReceived -= new DataReceivedEventHandler(outputHandler);
                    process.ErrorDataReceived -= new DataReceivedEventHandler(outputHandler);
                }
            }
            catch (Exception ex)
            {
                // if process is killed, this could throw an unhandled exception, if we try to print out process.id
                if (process != null)
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Process " + process.Id + " failed with exception:  " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Handle Exited event and display process information. 
        /// </summary>
        private void process_Exited(object sender, System.EventArgs e)
        {
            Console.WriteLine("Exit time:    {0}\r\n" + "Exit code:    {1}\r\n", process.ExitTime, process.ExitCode);
        }

        /// <summary>
        /// STDOUT/STDERR Handler
        /// </summary>
        /// <param name="sendingProcess">Source process</param>
        /// <param name="outLine">Output Line</param>
        private void outputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                outputList.Enqueue(outLine.Data);
            }
        }

        private Command checkoutCmd()
        {
            return
                new Command()
                    .Add("git reset --hard")
                    .Add("git checkout " + buildInfo.sha);
        }

        private Command cloneCmd()
        {
            return
                new Command()
                    .Add("git clone " + buildInfo.repo_url + " " + sProjectDir)
                    .Add("git checkout " + buildInfo.sha);
        }

        private Command fetchCmd()
        {
            return
                new Command()
                    .Add("git reset --hard")
                    .Add("git clean -fdx")
                    .Add("git remote set-url origin " + buildInfo.repo_url)
                    .Add("git fetch origin");
        }

        /// <summary>
        /// Delete non empty directory tree
        /// </summary>
        private void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (PathTooLongException)
                {
                    LongPathFile.Delete(file);
                }
            }

            foreach (string dir in dirs)
            {
                // Only recurse into "normal" directories
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    try
                    {
                        Directory.Delete(dir, false);
                    }
                    catch (PathTooLongException)
                    {
                        LongPathDirectory.Delete(dir);
                    }
                else
                    DeleteDirectory(dir);
            }

            try
            {
                Directory.Delete(target_dir, false);
            }
            catch (PathTooLongException)
            {
                LongPathDirectory.Delete(target_dir);
            }
        }
    }
}
