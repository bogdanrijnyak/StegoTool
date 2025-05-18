using System;
using System.Collections.Generic;
using System.Text;

namespace Stegonagraph
{
    //JPEG дерево хафмана
    class HuffTree
    {
        public int Code { set; get; }
        public int Val { set; get; }

        public HuffTree(int code, int val)
        {
            Code = code;
            Val = val;
        }

    }
}
