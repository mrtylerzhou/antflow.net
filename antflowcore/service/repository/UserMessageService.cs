﻿using AntFlowCore.Entity;

namespace antflowcore.service.repository;

public class UserMessageService :AFBaseCurdRepositoryService<UserMessage>
{
    public UserMessageService(IFreeSql freeSql) : base(freeSql)
    {
    }
    
    public void ReadNode(string node)
    {
        List<UserMessage> userMessages = this.baseRepo.Where(a=>a.Node==node).ToList();
        foreach (UserMessage userMessage in userMessages)
        {
            userMessage.IsRead=true;
            this.baseRepo.Update(userMessage);
        }
    }
}