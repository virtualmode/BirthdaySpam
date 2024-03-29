﻿// ---------------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Elcomplus LLC"></copyright>
// <date>2018-06-19</date>
// <summary>Birthday spam program.</summary>
// ---------------------------------------------------------------------------------------------------------------------------------------------------
namespace BirthdaySpam
{
    using System;
    using System.Globalization;

    using Code;

    class Program
    {
        #region Fields

        private static BirthdayController _birthdayController;

        #endregion Fields

        #region Methods

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Set default culture info to prevent problems on different platforms:
            var cultureInfo = new CultureInfo(BirthdayController.CULTURE_INFO_RU_RU);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            // Initialize birthday controller:
            Console.WriteLine("Birthday Spam 1.0");
            _birthdayController = new BirthdayController();
            _birthdayController.Start();

            Console.WriteLine($"Service started at {_birthdayController.Address}");
            Console.WriteLine("Press <Q> then <Enter> to stop service.");
            Console.WriteLine();

            int key; // Current user command.
            while ((key = Console.Read()) != 113 && key != 81) // Awaiting Q.
            {
                switch(key)
                {
                    case 10: // Enter. Nothing to do.
                    case 13:
                        break;

                    default:
                        Console.WriteLine($"Unknown command coed = {key}.");
                        break;
                }
            }

            Exit();
        }

        private static void Exit()
        {
            Console.WriteLine("Stopping service...");
            _birthdayController.Stop();
        }

        #endregion Methods
    }
}
