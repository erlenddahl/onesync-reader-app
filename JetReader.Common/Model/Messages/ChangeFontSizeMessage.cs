﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetReader.Model.Messages {
    public class ChangeFontSizeMessage {
        public double FontSize { get; set; }

        public ChangeFontSizeMessage(double fontSize)
        {
            FontSize = fontSize;
        }
    }
}
