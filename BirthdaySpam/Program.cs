// ---------------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Elcomplus LLC"></copyright>
// <date>2018-06-19</date>
// <summary>Birthday spam program.</summary>
// ---------------------------------------------------------------------------------------------------------------------------------------------------
namespace BirthdaySpam
{
    using System;
    using System.Diagnostics;

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
            // Initialize birthday controller:
            _birthdayController = new BirthdayController();
            _birthdayController.Start();

            Console.WriteLine("Hello World!");
        }

        private static void Open()
        {
            Process.Start($"http://localhost:{_birthdayController.HttpPort}");
        }

        private static void Exit()
        {
            _birthdayController.Stop();
        }

        #endregion Methods
    }
}
