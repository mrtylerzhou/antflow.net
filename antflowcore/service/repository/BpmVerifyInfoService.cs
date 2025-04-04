﻿using System.Linq.Expressions;
using antflowcore.entity;
using AntFlowCore.Entity;
using antflowcore.service.biz;
using antflowcore.util;
using AntFlowCore.Vo;

namespace antflowcore.service.repository;

public class BpmVerifyInfoService: AFBaseCurdRepositoryService<BpmVerifyInfo>
{
    private readonly BpmFlowrunEntrustService _bpmFlowrunEntrustService;
    private readonly BpmBusinessProcessService _bpmBusinessProcessService;
    private readonly AFTaskService _afTaskService;
    private readonly ProcessConstantsService _processConstantsService;
    private readonly BpmVariableService _bpmVariableService;
    private readonly BpmVariableSignUpService _bpmVariableSignUpService;
    private readonly IBpmnEmployeeInfoProviderService _bpmnEmployeeInfoProviderService;
    private readonly BpmnNodeService _nodeService;

    public BpmVerifyInfoService(
        BpmFlowrunEntrustService bpmFlowrunEntrustService,
        BpmBusinessProcessService bpmBusinessProcessService,
        AFTaskService afTaskService,
        ProcessConstantsService processConstantsService,
        BpmVariableService bpmVariableService,
        BpmVariableSignUpService bpmVariableSignUpService,
        IBpmnEmployeeInfoProviderService bpmnEmployeeInfoProviderService,
        BpmnNodeService nodeService,
        IFreeSql freeSql
    ) : base(freeSql)
    {
        _bpmFlowrunEntrustService = bpmFlowrunEntrustService;
        _bpmBusinessProcessService = bpmBusinessProcessService;
        _afTaskService = afTaskService;
        _processConstantsService = processConstantsService;
        _bpmVariableService = bpmVariableService;
        _bpmVariableSignUpService = bpmVariableSignUpService;
        _bpmnEmployeeInfoProviderService = bpmnEmployeeInfoProviderService;
        _nodeService = nodeService;
    }

    public void AddVerifyInfo(BpmVerifyInfo verifyInfo)
    {
        BpmFlowrunEntrust entrustByTaskId = _bpmFlowrunEntrustService.GetEntrustByTaskId(SecurityUtils.GetLogInEmpIdStr(), verifyInfo.RunInfoId, verifyInfo.TaskId);
    }

    public string FindCurrentNodeIds(string processNumber)
    {
        // 查询业务流程信息
        BpmBusinessProcess bpmBusinessProcess =
            _bpmBusinessProcessService.baseRepo.Where(a => a.BusinessNumber == processNumber).First();
        
        if (bpmBusinessProcess == null)
        {
            return string.Empty;
        }

        // 获取 act_ru_task 表的 PROC_INST_ID_
        string procInstId = bpmBusinessProcess.ProcInstId;

        var tasks = FindTaskInfo(procInstId) ?? new List<BpmVerifyInfoVo>();
        if (tasks.Count == 0)
        {
            return string.Empty;
        }

        string elementId = tasks[0].ElementId;
        var bpmnNodeIds = _bpmVariableService.GetNodeIdsByeElementId(processNumber, elementId);

        if (bpmnNodeIds == null || bpmnNodeIds.Count == 0)
        {
            BpmAfTaskInst prevTask = _processConstantsService.GetPrevTask(elementId, procInstId);
            if (prevTask != null)
            {
                string taskDefinitionKey = prevTask.TaskDefKey;
                bpmnNodeIds = _bpmVariableSignUpService
                    .GetSignUpPrevNodeIdsByeElementId(processNumber, taskDefinitionKey);
            }
        }

        if (bpmnNodeIds == null || bpmnNodeIds.Count == 0)
        {
            return string.Empty;
        }


        List<BpmnNode> bpmnNodes = _nodeService.baseRepo.Where(a => bpmnNodeIds.Contains(a.Id.ToString())).ToList();
        if (bpmnNodes == null || bpmnNodes.Count == 0)
        {
            return string.Empty;
        }

        var nodeCollect = bpmnNodes.Select(node => node.NodeId).ToList();
        return string.Join(",", nodeCollect);
    }

    public List<BpmVerifyInfoVo> FindTaskInfo(String procInstId)
    {
        List<BpmVerifyInfoVo> bpmVerifyInfos = _afTaskService.baseRepo.Where(a => a.ProcInstId == procInstId).ToList().Select(t => new BpmVerifyInfoVo()
            {
                Id = t.Id,
                TaskName = t.Name,
                VerifyUserId = t.Assignee,
                VerifyUserName = t.AssigneeName,
                VerifyStatusName = "处理中" ,
                ElementId = t.TaskDefKey,
                VerifyDesc = "",
                VerifyDate = null,
            }
        ).ToList();
        return bpmVerifyInfos;
    }
    public List<BpmVerifyInfoVo> FindTaskInfo(BpmBusinessProcess bpmBusinessProcess)
    {
        string procInstId = bpmBusinessProcess.ProcInstId;
        var tasks = this.FindTaskInfo(procInstId);

        if (!tasks.Any())
        {
            return new List<BpmVerifyInfoVo>();
        }

        var verifyUserIds = tasks.Select(t => t.VerifyUserId).ToList();
        int? isOutSideProcess = bpmBusinessProcess.IsOutSideProcess;
        Dictionary<string, string> stringStringMap = null;

        if (isOutSideProcess == 1)
        {
            stringStringMap = tasks.ToDictionary(t => t.VerifyUserId, t => t.VerifyUserName,StringComparer.Ordinal);
        }
        else
        {
            stringStringMap = _bpmnEmployeeInfoProviderService.ProvideEmployeeInfo(verifyUserIds);
        }

        foreach (var task in tasks)
        {
            if (stringStringMap.ContainsKey(task.VerifyUserId))
            {
                task.VerifyUserName = stringStringMap[task.VerifyUserId];
            }
        }

        var taskInfors = new List<BpmVerifyInfoVo>();

        if (tasks.Count > 1)
        {
            string verifyUserName = string.Join(",", tasks.Select(t => t.VerifyUserName));
            string taskName = string.Empty;
            var strs = tasks.Select(t => t.TaskName).Where(t => t != null).ToList();

            if (strs.Any())
            {
                taskName = string.Join("||", strs);
            }

            string elementId = string.Join(",", tasks.Select(t => t.ElementId));

            taskInfors.Add(new BpmVerifyInfoVo
            {
                VerifyUserIds = verifyUserIds,
                VerifyUserName = verifyUserName,
                TaskName = taskName,
                ElementId = elementId
            });
        }
        else
        {
            tasks[0].VerifyUserIds = verifyUserIds;
            taskInfors.Add(tasks[0]);
        }

        return taskInfors;
    }

    public List<BpmVerifyInfoVo> VerifyInfoList(String processNumber,String procInstId)
    {
        BpmVerifyInfoVo vo = new BpmVerifyInfoVo()
        {
            ProcessCode = processNumber
        };
        List<BpmVerifyInfoVo> bpmVerifyInfoVos = GetVerifyInfo(vo);
        return GetBpmVerifyInfoVoList(bpmVerifyInfoVos, procInstId);
    }

    private List<BpmVerifyInfoVo> GetVerifyInfo(BpmVerifyInfoVo vo)
    {
       
        Expression<Func<BpmVerifyInfo, bool>> expression = a => true;
        if (!string.IsNullOrEmpty(vo.ProcessCode))
        {
            expression = expression.And(a => a.ProcessCode == vo.ProcessCode);
        }

        if (vo.ProcessCodeList != null && vo.ProcessCodeList.Count > 0)
        {
            expression.And(a => vo.ProcessCodeList.Contains(a.ProcessCode));
        }

        if (!string.IsNullOrEmpty(vo.BusinessId))
        {
            expression = expression.And(a => a.BusinessId == vo.BusinessId);
        }


        List<BpmVerifyInfoVo> bpmVerifyInfoVos = baseRepo
            .Where(expression)
            .ToList<BpmVerifyInfoVo>(w => new BpmVerifyInfoVo
            {
                Id = w.Id.ToString(),
                VerifyUserId = w.VerifyUserId,
                VerifyUserName = w.VerifyUserName,
                TaskName = w.TaskName,
                VerifyStatus = w.VerifyStatus,
                VerifyStatusName = GetVerifyStatusName(w.VerifyStatus),
                VerifyDate = w.VerifyDate,
                VerifyDesc = w.VerifyDesc,
                OriginalId = w.OriginalId,
            }).OrderByDescending(a => a.VerifyDate)
            .ToList();
        return bpmVerifyInfoVos;
    }
    private string GetVerifyStatusName(int verifyStatus)
    {
        string verifyStatusName;

        switch (verifyStatus)
        {
            case 1:
                verifyStatusName = "提交";
                break;
            case 2:
                verifyStatusName = "同意";
                break;
            case 3:
                verifyStatusName = "不同意";
                break;
            case 4:
                verifyStatusName = "撤回";
                break;
            case 5:
                verifyStatusName = "作废";
                break;
            case 6:
                verifyStatusName = "终止";
                break;
            case 8:
                verifyStatusName = "退回修改";
                break;
            case 9:
                verifyStatusName = "加批";
                break;
            default:
                verifyStatusName = "";
                break;
        }

        return verifyStatusName;
    }

    public List<BpmVerifyInfoVo> GetBpmVerifyInfoVoList(List<BpmVerifyInfoVo> list, string procInstId)
    {
        var infoVoList = new List<BpmVerifyInfoVo>();

        infoVoList.AddRange(list.Select(o =>
        {
            if (!string.IsNullOrEmpty(o.OriginalId))
            {
                if (!string.IsNullOrEmpty(procInstId))
                {
                    List<BpmFlowrunEntrust> bpmFlowrunEntrusts = _bpmFlowrunEntrustService
                        .baseRepo
                        .Where(a=>a.Original==o.OriginalId&&a.RunInfoId==o.RunInfoId)
                        .ToList();

                    if (bpmFlowrunEntrusts != null && bpmFlowrunEntrusts.Any())
                    {
                        o.OriginalName = bpmFlowrunEntrusts[0].OriginalName;
                        o.VerifyUserName = $"{o.VerifyUserName} 代 {o.OriginalName} 审批";
                    }
                }
                else
                {
                    var employeeInfo = _bpmnEmployeeInfoProviderService.ProvideEmployeeInfo(new List<string> { o.OriginalId });
                    if (employeeInfo.TryGetValue(o.OriginalId, out var originalName))
                    {
                        o.OriginalName = originalName;
                        o.VerifyUserName = $"{o.VerifyUserName} 代 {o.OriginalName} 审批";
                    }
                }
            }
            return o;
        }).ToList());

        return infoVoList;
    }

}