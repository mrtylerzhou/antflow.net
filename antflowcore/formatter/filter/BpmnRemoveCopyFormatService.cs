﻿using System.Collections.ObjectModel;
using antflowcore.constant.enus;
using antflowcore.vo;
using AntFlowCore.Vo;

namespace antflowcore.service.processor.filter;

public class BpmnRemoveCopyFormatService: AbstractBpmnRemoveFormat
{
    public new void RemoveBpmnConf(BpmnConfVo bpmnConfVo, BpmnStartConditionsVo bpmnStartConditions)
    {
        base.RemoveBpmnConf(bpmnConfVo, bpmnStartConditions);
    }

    public override List<Func<bool>> TrueFuncs(BpmnNodeVo vo, BpmnStartConditionsVo bpmnStartConditions)
    {
        Func<bool> func = () => vo.NodeType == (int)NodeTypeEnum.NODE_TYPE_COPY;
        if (func())
        {
            if (bpmnStartConditions.EmpToForwardList == null)
                bpmnStartConditions.EmpToForwardList = new List<string>();

            bpmnStartConditions.EmpToForwardList.AddRange(vo.Property.EmplIds);
        }
        return new List<Func<bool>> { func };
    }

    public override int Order()
    {
        return 2;
    }
}