// Project OldRod - A KoiVM devirtualisation utility.
// Copyright (C) 2019 Washi
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Text;
using AsmResolver.DotNet.Code.Cil;
using OldRod.Core.Ast.Cil;
using OldRod.Core.Ast.IL;
using OldRod.Core.Disassembly.ControlFlow;
using Rivers.Serialization.Dot;

namespace OldRod.Pipeline
{
    internal class BasicBlockSerializer : IUserDataSerializer
    {
        private readonly CilAstFormatter _formatter;
        private readonly DefaultUserDataSerializer _default = new DefaultUserDataSerializer();

        public BasicBlockSerializer()
        {
        }
        
        public BasicBlockSerializer(CilMethodBody methodBody)
        {
            _formatter = new CilAstFormatter(methodBody);
        }
        
        public string Serialize(string attributeName, object attributeValue)
        {
            switch (attributeValue)
            {
                case ILBasicBlock basicBlock:
                    return ToLinesWithMaxLength(basicBlock.Instructions);
                case ILAstBlock ilAstBlock:
                    return ToLinesWithMaxLength(ilAstBlock.Statements);
                case CilAstBlock cilAstBlock when _formatter != null:
                    return cilAstBlock.AcceptVisitor(_formatter);
                case CilAstBlock cilAstBlock:
                    return ToLinesWithMaxLength(cilAstBlock.Statements);
                default:
                    return _default.Serialize(attributeName, attributeValue);
            }
        }

        public object Deserialize(string attributeName, string rawValue)
        {
            return _default.Deserialize(attributeName, rawValue);
        }

        public static string ToLinesWithMaxLength(IEnumerable<object> values, int maxLineLength = 100)
        {
            var builder = new StringBuilder();

            foreach (var value in values)
            {
                string stringValue = value.ToString();

                for (int i = 0; i < stringValue.Length; i += maxLineLength)
                {
                    int lineLength = Math.Min(stringValue.Length - i, maxLineLength);
                    string line = stringValue.Substring(i, lineLength);
                    if (i > 0)
                        builder.Append("     ");
                    builder.Append(line);
                    builder.Append("\\l");
                }
            }
            
            return builder.ToString();
        }
    }
}