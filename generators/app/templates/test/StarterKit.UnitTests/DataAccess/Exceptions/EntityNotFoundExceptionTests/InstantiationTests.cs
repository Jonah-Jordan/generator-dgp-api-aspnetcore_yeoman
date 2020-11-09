﻿using Digipolis.DataAccess.Exceptions;
using Xunit;

namespace StarterKit.UnitTests.DataAccess.Exceptions.EntityNotFoundExceptionTests
{
  public class InstantiationTests
  {
    [Fact]
    private void EntityNameIsSet()
    {
      var ex = new EntityNotFoundException("entity", 123);
      Assert.Equal("entity", ex.EntityName);
    }

    [Fact]
    private void EntityKeyIsSet()
    {
      var ex = new EntityNotFoundException("entity", 123);
      Assert.Equal(123, ex.EntityKey);
    }
  }
}
