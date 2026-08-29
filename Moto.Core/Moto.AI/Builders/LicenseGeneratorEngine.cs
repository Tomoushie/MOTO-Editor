// Moto.Core/AI/Builders/LicenseGeneratorEngine.cs
using System;
using System.Collections.Generic;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Builders
{
    /// <summary>
    /// Génère automatiquement des licences libres (MIT, BSD, Apache, GPL).
    /// </summary>
    public class LicenseGeneratorEngine
    {
        public string[] AvailableLicenses => new[] { "MIT", "BSD-3-Clause", "Apache-2.0", "GPL-3.0" };

        public List<AiFileChange> Generate(string licenseId, string author, string projectName)
        {
            var year = DateTime.Now.Year;

            var changes = new List<AiFileChange>();

            changes.Add(new AiFileChange
            {
                Path = "LICENSE",
                Reason = $"Licence {licenseId} générée.",
                ChangeType = FileChangeType.Create,
                Content = GetLicenseText(licenseId, author, year, projectName)
            });

            return changes;
        }

        private string GetLicenseText(string id, string author, int year, string project)
        {
            return id switch
            {
                "MIT" => MitText(author, year),
                "BSD-3-Clause" => BsdText(author, year),
                "Apache-2.0" => ApacheNotice(author, year, project),
                "GPL-3.0" => GplNotice(author, year, project),
                _ => MitText(author, year)
            };
        }

        private string MitText(string author, int year)
        {
            return $@"MIT License

Copyright (c) {year} {author}

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";
        }

        private string BsdText(string author, int year)
        {
            return $@"BSD 3-Clause License

Copyright (c) {year}, {author}
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors
   may be used to endorse or promote products derived from this software
   without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ""AS IS""
AND ANY EXPRESS OR IMPLIED WARRANTIES ARE DISCLAIMED.";
        }

        // Apache/GPL : texte officiel très long. On génère l'avis standard
        // + pointeur. Le texte complet peut être embarqué en ressource.
        private string ApacheNotice(string author, int year, string project)
        {
            return $@"{project}

Copyright {year} {author}

Licensed under the Apache License, Version 2.0 (the ""License"");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an ""AS IS"" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.";
        }

        private string GplNotice(string author, int year, string project)
        {
            return $@"{project}
Copyright (C) {year} {author}

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details:
https://www.gnu.org/licenses/gpl-3.0.txt";
        }
    }
}
