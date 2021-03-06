﻿using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
using TestUtility;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class AbstractAutoCompleteTestFixture : AbstractSingleRequestHandlerTestFixture<IntellisenseService>
    {
        protected AbstractAutoCompleteTestFixture(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmnisharpEndpoints.AutoComplete;

        protected async Task<IEnumerable<AutoCompleteResponse>> FindCompletionsAsync(string source, bool wantSnippet = false)
        {
            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var point = testFile.Content.GetPointFromPosition();

                var request = new AutoCompleteRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Buffer = testFile.Content.Code,
                    WordToComplete = GetPartialWord(testFile.Content),
                    WantMethodHeader = true,
                    WantSnippet = wantSnippet,
                    WantReturnType = true
                };

                var requestHandler = GetRequestHandler(host);

                return await requestHandler.Handle(request);
            }
        }

        private static string GetPartialWord(TestContent testConnect)
        {
            if (!testConnect.HasPosition || testConnect.Position == 0)
            {
                return string.Empty;
            }

            var index = testConnect.Position;
            while (index >= 1)
            {
                var ch = testConnect.Code[index - 1];
                if (ch != '_' && !char.IsLetterOrDigit(ch))
                {
                    break;
                }

                index--;
            }

            return testConnect.Code.Substring(index, testConnect.Position - index);
        }
    }
}
