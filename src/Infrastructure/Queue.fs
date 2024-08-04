module Infrastructure.Queue

type BaseMessage<'a> = { OperationId: string; Data: 'a }

