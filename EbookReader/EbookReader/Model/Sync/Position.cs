﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EbookReader.Model.Bookshelf;
using Newtonsoft.Json;

namespace EbookReader.Model.Sync {
    public class Progress {

        [JsonIgnore]
        public string DeviceName {
            get => D;
            set => D = value;
        }

        [JsonIgnore]
        public Position Position {
            get => new Position(S, P);
            set {
                S = value.Spine;
                P = value.SpinePosition;
            }
        }

        public string D { get; private set; }

        public int S { get; private set; }

        public int P { get; private set; }
    }
}
