﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using gitlab_ci_runner.api;
using gitlab_ci_runner.helper;

namespace gitlab_ci_runner.runner
{
    class Runner
    {
        /// <summary>
        /// Build process
        /// </summary>
        private static Build build = null;
        private static DateTime buildstarttime;

        /// <summary>
        /// Start the configured runner
        /// </summary>
        public static void run()
        {
            Console.WriteLine("* Gitlab CI Runner started");
            Console.WriteLine("* Waiting for builds");
            Console.WriteLine("----------------------------------------------------------");
            waitForBuild();
        }

        /// <summary>
        /// Build completed?
        /// </summary>
        public static bool buildCompleted
        {
            get
            {
                return (build != null && build.state == State.SUCCESS);
            }
        }

        /// <summary>
        /// Build running?
        /// </summary>
        public static bool buildRunning
        {
            get
            {
                return (build != null && build.state == State.RUNNING);
            }
        }

        /// <summary>
        /// Wait for an incoming build or update current Build
        /// </summary>
        private static void waitForBuild()
        {
            while (true)
            {
                if (build != null && buildRunning)
                {
                    // Build is running
                    updateBuild();
                }
                else if (build != null)
                {
                    // Build failed to start, failed or was aborted
                    completeBuild();
                }
                else if (build == null)
                {
                    // Get new build
                    getBuild();
                }
                Thread.Sleep(5000);
            }
        }

        /// <summary>
        /// Final update the currently running build
        /// </summary>
        private static void completeBuild()
        {
            // Get build status
            State currentState = build.state;

            Console.Write("[{0}] Build {1} ", DateTime.Now, build.buildInfo.id);
            if (currentState == State.SUCCESS)
            {
                Console.WriteLine("completed.");
            }
            else if (currentState == State.FAILED)
            {
                Console.WriteLine("failed.");
            }
            else if (currentState == State.ABORTED)
            {
                Console.WriteLine("was aborted.");
            }
            else
            {
                Console.WriteLine("... Something went wrong when completing build.");
            }
            
            // Push the final status
            pushBuild();

            TimeSpan buildruntime = (DateTime.Now - buildstarttime);
            Console.WriteLine("[{0}] Build {1} runtime: {2,2}h {3,2}min {4,2}sec", DateTime.Now, build.buildInfo.id, buildruntime.Hours, buildruntime.Minutes, buildruntime.Seconds);
            Console.WriteLine("----------------------------------------------------------");

            build.terminate();
            build = null;
        }

        /// <summary>
        /// Update the current running build progress
        /// </summary>
        private static void updateBuild()
        {
            // Build is currently running
            State pushReturnState = pushBuild();

            if (pushReturnState == State.FAILED)
            {
                Console.WriteLine("[" + DateTime.Now.ToString() + "] * Failed to push build status.");
            }
            else if (pushReturnState == State.ABORTED)
            {
                Console.WriteLine("[" + DateTime.Now.ToString() + "] * Build was aborted remotely, stopping...");
        
                // We trigger build termination here. That will set the build status to ABORTED or FAILED.
                build.terminate();
            }
            else if (pushReturnState == State.SUCCESS)
            {
            }
            else
            {
                Console.WriteLine("[" + DateTime.Now.ToString() + "] * Invalid return state from PushBuild." + pushReturnState.ToString());
            }

        }

        /// <summary>
        /// PUSH Build Status to Gitlab CI
        /// </summary>
        /// <returns>true on success, false on fail</returns>
        private static State pushBuild()
        {
            return Network.pushBuild(build.buildInfo.id, build.state, build.output);
        }

        /// <summary>
        /// Get a new build job
        /// </summary>
        private static void getBuild()
        {
            BuildInfo binfo = Network.getBuild();
            
            // New build was requested
            if (binfo != null)
            {
                // Create Build Job
                build = new Build(binfo);
                Console.WriteLine("[" + DateTime.Now.ToString() + "] Build " + binfo.id + " started...");
                Thread t = new Thread(build.run);
                t.Name = build.buildInfo.id.ToString();
                t.Start();
                buildstarttime = DateTime.Now;
            }
        }
    }
}
