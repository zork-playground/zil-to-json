﻿/* Copyright 2010-2018 Jesse McGrew
 * 
 * This file is part of ZILF.
 * 
 * ZILF is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * ZILF is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with ZILF.  If not, see <http://www.gnu.org/licenses/>.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Zilf.Emit.Zap;

namespace Zilf.Emit.Tests
{
    [TestClass, TestCategory("Compiler")]
    public class EmitZapTests
    {
        MockRepository mockRepository;
        Mock<IZapStreamFactory> mockStreamFactory;

        [TestInitialize]
        public void Initialize()
        {
            mockRepository = new MockRepository(MockBehavior.Strict);

            mockStreamFactory = mockRepository.Create<IZapStreamFactory>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            mockRepository.VerifyAll();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), "zversion 0 should be rejected")]
        public void Ctor_Should_Reject_Low_Zversion()
        {
            var dummy = new GameBuilder(0, mockStreamFactory.Object, false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException), "zversion 9 should be rejected")]
        public void Ctor_Should_Reject_High_Zversion()
        {
            var dummy = new GameBuilder(9, mockStreamFactory.Object, false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException), "null streamfactory should be rejected")]
        public void Ctor_Should_Reject_Null_StreamFactory()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            var dummy = new GameBuilder(5, null, false);
        }
    }
}
