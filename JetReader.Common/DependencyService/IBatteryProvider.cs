﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetReader.DependencyService {
    public interface IBatteryProvider {
        int RemainingChargePercent { get; }
    }
}
