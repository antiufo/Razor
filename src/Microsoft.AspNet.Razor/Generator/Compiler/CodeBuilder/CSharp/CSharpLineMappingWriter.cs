﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Razor.Text;

namespace Microsoft.AspNet.Razor.Generator.Compiler.CSharp
{
    public class CSharpLineMappingWriter : IDisposable
    {
        private CSharpCodeWriter _writer;
        private MappingLocation _documentMapping;
        private SourceLocation _generatedLocation;
        private int _startIndent;
        private int _generatedContentLength;

        public CSharpLineMappingWriter(CSharpCodeWriter writer, SourceLocation documentLocation, int contentLength, string sourceFilename)
        {
            _writer = writer;
            _documentMapping = new MappingLocation(documentLocation, contentLength);

            _startIndent = _writer.CurrentIndent;
            _generatedContentLength = 0;
            _writer.ResetIndent();

            // TODO: Should this just be '\n'?
            if (_writer.LastWrite.Last() != '\n')
            {
                _writer.WriteLine();
            }

            _writer.WriteLineNumberDirective(documentLocation.LineIndex + 1, sourceFilename);

            _generatedLocation = _writer.GetCurrentSourceLocation();
        }

        public void MarkLineMappingStart()
        {
            _generatedLocation = _writer.GetCurrentSourceLocation();
        }

        public void MarkLineMappingEnd()
        {
            _generatedContentLength = _writer.ToString().Length - _generatedLocation.AbsoluteIndex;
        }

        public void Dispose()
        {
            // Verify that the generated length has not already been calculated
            if (_generatedContentLength == 0)
            {
                _generatedContentLength = _writer.ToString().Length - _generatedLocation.AbsoluteIndex;
            }
            
            var generatedLocation = new MappingLocation(_generatedLocation, _generatedContentLength);
            if(_documentMapping.ContentLength == -1)
            {
                _documentMapping.ContentLength = generatedLocation.ContentLength;
            }

            _writer.LineMappingManager.AddMapping(
                documentLocation: _documentMapping,
                generatedLocation: new MappingLocation(_generatedLocation, _generatedContentLength));

            if (_writer.LastWrite.Last() != '\n')
            {
                _writer.WriteLine();
            }

            _writer.WriteLineDefaultDirective();
            _writer.WriteLineHiddenDirective();

            // Reset indent back to when it was started
            _writer.SetIndent(_startIndent);
        }
    }
}