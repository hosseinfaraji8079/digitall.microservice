﻿using Domain.Enums;

namespace Application.Exceptions;

public class ImplementedException : AppException
{
    public ImplementedException()
        : base(ApiResultStatusCode.NotImplemented, System.Net.HttpStatusCode.NotImplemented)
    {
    }

    public ImplementedException(string message) : base(ApiResultStatusCode.NotImplemented, message)
    {

    }
}

