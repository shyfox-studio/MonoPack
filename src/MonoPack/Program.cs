// Copyright (c) ShyFox Studio. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using MonoPack;

try
{
    Options options = Options.FromArgs(args);
    MonoPackService service = new MonoPackService(options);
    service.Execute();
    return 0;
}
catch(Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
