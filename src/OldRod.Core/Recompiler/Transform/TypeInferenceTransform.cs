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
using System.Linq;
using AsmResolver.Net;
using AsmResolver.Net.Cts;
using AsmResolver.Net.Signatures;
using OldRod.Core.Ast.Cil;

namespace OldRod.Core.Recompiler.Transform
{
    public class TypeInferenceTransform : ChangeAwareCilAstTransform
    {
        private ITypeDefOrRef _arrayType;
        private RecompilerContext _context;

        public override string Name => "Type Inference";

        public override bool ApplyTransformation(RecompilerContext context, CilCompilationUnit unit)
        {
            _context = context;
            _arrayType = context.ReferenceImporter.ImportType(typeof(Array));
            return base.ApplyTransformation(context, unit);
        }

        private IList<ITypeDescriptor> GetTypeHierarchy(ITypeDescriptor type)
        {
            var result = new List<ITypeDescriptor>();
            
            ITypeDefOrRef typeDefOrRef;
            switch (type)
            {
                // The base type of an array type signature is System.Array, so it needs a special case. 
                // Get the type hierarchy of System.Array and then append the original array type sig.
                case ArrayTypeSignature _:
                case SzArrayTypeSignature _:
                    result.AddRange(GetTypeHierarchy(_arrayType));
                    result.Add(type);
                    return result;
                
                // Type specification's Resolve method resolves the underlying element type.
                // We therefore need a special case here, to get the type hierarchy of the embedded signature first.
                case TypeSpecification typeSpec:
                    result.AddRange(GetTypeHierarchy(typeSpec.Signature));
                    result.Add(typeSpec);
                    return result;
                
                // No type means no hierarchy.
                case null:
                    return Array.Empty<ITypeDescriptor>();
                
                // Default to a standard conversion to TypeDefOrRef.
                default:
                    typeDefOrRef = type.ToTypeDefOrRef();
                    break;
            }
            
            // Resolve and visit all base types.
            while (typeDefOrRef != null)
            {
                var typeDef = (TypeDefinition) typeDefOrRef.Resolve();
                result.Add(typeDef);
                typeDefOrRef = typeDef.BaseType;
            }

            result.Reverse();
            return result;
        }

        private ITypeDescriptor GetCommonBaseType(IEnumerable<ITypeDescriptor> types)
        {
            // Obtain all base types for all types.
            var hierarchies = types.Select(GetTypeHierarchy).ToArray();
            int shortestSequenceLength = hierarchies.Min(x => x.Count);
            
            // Find the maximum index for which the hierarchies are still the same.
            for (int i = 0; i < shortestSequenceLength; i++)
            {
                // If any of the types at the current position is different, we have found the index.
                if (hierarchies.Any(x => hierarchies[0][i] != x[i]))
                    return i == 0 ? null : hierarchies[0][i - 1];
            }
            
            // We've walked over all hierarchies, just pick the last one of the shortest hierarchy.
            return shortestSequenceLength > 0 
                ? hierarchies[0][shortestSequenceLength - 1] 
                : null;
        }
        
        public override bool VisitCompilationUnit(CilCompilationUnit unit)
        {
            bool changed = false;
            
            // Go over each variable, and figure out the common base type of all the values that are assigned to it.
            // This is the new variable type.
            foreach (var variable in unit.Variables.Where(x => x.AssignedBy.Count > 0))
            {
                var expressionTypes = variable.AssignedBy.Select(x => x.Value.ExpressionType).ToArray();
                var commonBaseType = GetCommonBaseType(expressionTypes);

                if (commonBaseType != null && variable.Signature.VariableType.FullName != commonBaseType.FullName)
                {
                    var newType = _context.TargetImage.TypeSystem.GetMscorlibType(commonBaseType) 
                                  ?? _context.ReferenceImporter.ImportTypeSignature(commonBaseType.ToTypeSignature());
                    variable.Signature.VariableType = newType;

                    // Update the expression type of all references to the variable.
                    foreach (var use in variable.UsedBy)
                        use.ExpressionType = newType;
                    
                    changed = true;
                }
            }
            
            return changed;
        }
        
    }
}