﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EbookReader.Model.EpubLoader {
    public class Base64Image {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Data { get; set; }
    }
}
