﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using gitlab_ci_runner.api;
using gitlab_ci_runner.conf;
using gitlab_ci_runner.runner;
using ServiceStack;
using ServiceStack.Text;

namespace gitlab_ci_runner.helper
{
    class Network
    {
        /// <summary>
        /// Gitlab CI API URL
        /// </summary>
        private static string apiurl
        {
            get
            {
                return Config.url + "/api/v1/";
            }
        }

        /// <summary>
        /// Register the runner with the coordinator
        /// </summary>
        /// <param name="sPubKey">SSH Public Key</param>
        /// <param name="sToken">Token</param>
        /// <returns>Token</returns>
        public static string registerRunner(String sToken)
        {
			var client = new JsonServiceClient (apiurl);
			try
			{
				var authToken = client.Post (new RegisterRunner
				{
					token = Uri.EscapeDataString (sToken)
				});

				if (!authToken.token.IsNullOrEmpty ())
				{
						Console.WriteLine ("Runner registered with id {0}", authToken.id);
						return authToken.token;
				}
			}
			catch(WebException ex)
			{
				Console.WriteLine ("Error while registering runner :", ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Network:registerRunner] Error: {0}\n{1}", ex.Message, ex.InnerException);
            }
            return null;
        }

        /// <summary>
        /// Get a new build
        /// </summary>
        /// <returns>BuildInfo object or null on error/no build</returns>
        public static BuildInfo getBuild()
        {
			var client = new JsonServiceClient (apiurl);
            try {
                var buildInfo = client.Post(new CheckForBuild {
                    token = Uri.EscapeDataString(Config.token)
                });

                if (buildInfo != null) {
                    return buildInfo;
                }
            } catch (WebServiceException ex) {
                if (ex.StatusCode != 404)
                    Console.WriteLine("* Failed");

            }
            catch (Exception ex)
            {
                Console.WriteLine("[Network:getBuild] Error: {0}\n{1}", ex.Message, ex.InnerException);
            }

            return null;
        }

        /// <summary>
        /// PUSH the Build to the Gitlab CI Coordinator
        /// </summary>
        /// <param name="iId">Build ID</param>
        /// <param name="state">State</param>
        /// <param name="sTrace">Command output</param>
        /// <returns></returns>
        public static State pushBuild(int iId, State state, string sTrace)
        {
            State returnState = State.FAILED;
            bool shrinkOutput = true;       // Maybe make it configurable somewhere
            bool printTestResults = true;   // Maybe make it configurable somewhere

            var stateValue = "";
            if (state == State.RUNNING)
            {
                stateValue = "running";
            }
            else if (state == State.SUCCESS)
            {
                stateValue = "success";
            }
            else if (state == State.FAILED)
            {
                stateValue = "failed";
            }
            else if (state == State.ABORTED)
            {
                stateValue = "aborted";
            }
            else
            {
                stateValue = "waiting";
            }

            if (state == State.SUCCESS || state == State.FAILED || state == State.ABORTED)
            {
                shrinkOutput = false;
            }

            var trace = OutputParser.prettify(sTrace, shrinkOutput, printTestResults);

            int iTry = 0;
            try
            {
                while (iTry <= 5)
                {
                    var client = new JsonServiceClient(apiurl);
                    var resp = client.Put (new PushBuild {
                        id = iId + ".json",
                        token = Uri.EscapeDataString(Config.token),
                        state = stateValue,
                        trace = trace.ToString () });

                    if (state == State.RUNNING && resp != null && resp == "null")
                    {
                        returnState = State.SUCCESS;
                        break;
                    }else if (resp != null && resp == "true")
                    {
                        returnState = state;
                        break;
                    }

                    iTry++;
                    Thread.Sleep(1000);
                }
            }
            catch (WebServiceException ex)
            {
                switch (ex.StatusCode)
                {
                    case 200:
                        returnState = State.SUCCESS;
                        break;
                    case 404:
                        returnState = State.ABORTED;
                        break;
                    default:
                        returnState = State.FAILED;
                        Console.WriteLine("[" + DateTime.Now.ToString() + "] Got response when pushing build status [{0}]: {1}", returnState.ToString(), ex.Message);
                        break;
                }
            } catch (Exception ex)
            {
                Console.WriteLine("[Network:pushBuild] Error: {0}\n{1}",ex.Message,ex.InnerException);
            }
            return returnState;
        }
    }
}
