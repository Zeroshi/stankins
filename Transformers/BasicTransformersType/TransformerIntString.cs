﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Transformers.BasicTransformersType
{
    public class TransformerIntString : TransformerChangeType<int, string>
    {
        public TransformerIntString(string oldField, string newField) 
            : base(oldField, newField)
        {
        }
    }
}
