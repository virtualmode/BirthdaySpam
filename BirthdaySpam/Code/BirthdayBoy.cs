// ---------------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="BirthdayBoy.cs" company="Elcomplus LLC"></copyright>
// <date>2018-09-24</date>
// ---------------------------------------------------------------------------------------------------------------------------------------------------
namespace BirthdaySpam.Code
{
    using System;
    using System.Collections.Generic;

    public class BirthdayBoy
    {
        #region Fields

        public string Id; // Identifier for navigation.
        public string Name; // Birthday boy name.
        public string Email; // E-mail that will be used to send information about birthday.
        public DateTime Birthday; // Birthday boy day.
        public double Fee; // Collected funds.
        public string Val; // Val field for various purposes.

        public readonly List<BirthdayBoy> Subscribers; // Birthday subscribers.

        #endregion Fields

        #region Constructors

        public BirthdayBoy()
        {
            Subscribers = new List<BirthdayBoy>();
        }

        #endregion Constructors

        #region Methods

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return ((BirthdayBoy)obj)?.Id == Id;
        }

        #endregion Methods
    }
}
