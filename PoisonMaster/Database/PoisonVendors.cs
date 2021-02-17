﻿using System;
using System.Collections.Generic;
using robotManager.Helpful;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PoisonMaster;
using wManager.Wow.Enums;

public class PoisonVendors
{
    public static List<PoisonNPC> PoisonVendorList { get; private set; }

    private static readonly List<PoisonNPC> PoisonVendor = new List<PoisonNPC>()
    {
        new PoisonNPC(1, new Vector3(111,222,333), "Jonny", (ContinentId)1)
    };

    public static void ChoosePoisonVendorList()
    {
        PoisonVendorList = PoisonVendor;
    }
}

