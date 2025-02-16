﻿using Domain.Entities.Registry;
using Domain.IRepositories.Base;

namespace Domain.IRepositories.Registry;

public interface IRegistryRepository : IBaseRepository<Entities.Registry.Registry>;
public interface IRegistrationOptionsRepository : IBaseRepository<RegistrationOptions>;