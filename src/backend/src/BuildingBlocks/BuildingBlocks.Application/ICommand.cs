using MediatR;

namespace BuildingBlocks.Application;

public interface ICommand : IRequest<Unit> { }

public interface ICommand<TResponse> : IRequest<TResponse> { }

public interface IQuery<TResponse> : IRequest<TResponse> { }
