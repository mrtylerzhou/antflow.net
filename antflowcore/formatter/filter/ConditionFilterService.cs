﻿using System.Text.Json;
using antflowcore.constant.enus;
using antflowcore.exception;
using antflowcore.util;
using antflowcore.vo;
using AntFlowCore.Vo;
using Microsoft.Extensions.Logging;

namespace antflowcore.service.processor.filter;

//todo
public class ConditionFilterService
{
    private readonly IConditionService _conditionService;
    private readonly ILogger<ConditionFilterService> _logger;

    public ConditionFilterService(IConditionService conditionService, ILogger<ConditionFilterService> logger)
    {
        _conditionService = conditionService;
        _logger = logger;
    }

    public void ConditionfilterNode(BpmnConfVo bpmnConfVo, BpmnStartConditionsVo bpmnStartConditionsVo)
    {
        List<BpmnNodeVo> nodeList = bpmnConfVo.Nodes;
        if (ObjectUtils.IsEmpty(nodeList))
        {
            _logger.LogWarning("bpmn conf has no nodes");
            return;
        }

        Dictionary<String, BpmnNodeVo> nodeIdMapNode = new Dictionary<string, BpmnNodeVo>(16);
        BpmnNodeVo startNode = GetNodeMapAndStartNode(nodeList, nodeIdMapNode);
        if (startNode == null)
        {
            _logger.LogError("process has no start user,bpmnCode:{}", bpmnConfVo.BpmnCode);
            throw new AFBizException("999", "process has no start user");
        }

        //filter by conditions
        List<BpmnNodeVo> filterNodes = FilterNode(startNode, nodeIdMapNode, bpmnStartConditionsVo);
        bpmnConfVo.Nodes = filterNodes;
    }

    private List<BpmnNodeVo> FilterNode(BpmnNodeVo startNode, Dictionary<String, BpmnNodeVo> nodeIdMapNode,
        BpmnStartConditionsVo bpmnStartConditionsVo)
    {
        List<BpmnNodeVo> nodeList = new List<BpmnNodeVo>();
        BpmnNodeParamsVo parameters = new BpmnNodeParamsVo();
        parameters.NodeTo = startNode.NodeTo[0];
        startNode.Params = parameters;
        String nextId = parameters.NodeTo;
        do
        {
            if ((int)NodeTypeEnum.NODE_TYPE_PARALLEL_GATEWAY == (startNode.NodeType))
            {
                BpmnNodeVo aggregationNode = BpmnUtils.GetAggregationNode(startNode, nodeIdMapNode.Values);
                TreatParallelGatewayRecursively(startNode, aggregationNode, nodeIdMapNode, nodeList,
                    bpmnStartConditionsVo);
                nextId = aggregationNode.NodeId;
            }
            else
            {
                RecursionTreate(startNode, nodeIdMapNode, nodeList, bpmnStartConditionsVo);
                nextId = startNode.Params.NodeTo;
            }

            if (!string.IsNullOrEmpty(nextId))
            {
                startNode = nodeIdMapNode[nextId];
            }
        } while (!String.IsNullOrEmpty(nextId));

        List<BpmnNodeVo> list = DeleteConditionNode(nodeList);
        //check finally nodes
        CheckNode(list);
        return list;
    }

    public void TreatParallelGatewayRecursively(
        BpmnNodeVo outerMostParallelGatewayNode,
        BpmnNodeVo itsAggregationNode,
        Dictionary<string, BpmnNodeVo> nodeIdMapNode,
        List<BpmnNodeVo> nodeList,
        BpmnStartConditionsVo bpmnStartConditionsVo)
    {
        if (itsAggregationNode == null)
        {
            throw new AFBizException("There is a parallel gateway node, but cannot get its aggregation node!");
        }

        RecursionTreate(outerMostParallelGatewayNode, nodeIdMapNode, nodeList, bpmnStartConditionsVo);
        string aggregationNodeNodeId = itsAggregationNode.NodeId;
        List<string> nodeTos = outerMostParallelGatewayNode.NodeTo;

        foreach (string nodeTo in nodeTos)
        {
            if (!nodeIdMapNode.TryGetValue(nodeTo, out var currentNodeVo))
            {
                continue;
            }

            // Treat all nodes between parallel gateway and its aggregation node (not including the latter)
            for (var nodeVo = currentNodeVo;
                 nodeVo != null && nodeVo.NodeId != aggregationNodeNodeId;
                 nodeVo = nodeIdMapNode.TryGetValue(nodeVo.Params.NodeTo, out var nextNode) ? nextNode : null)
            {
                if ((int)NodeTypeEnum.NODE_TYPE_PARALLEL_GATEWAY==currentNodeVo.NodeType)
                {
                    var aggregationNode = BpmnUtils.GetAggregationNode(currentNodeVo, nodeIdMapNode.Values);
                    TreatParallelGatewayRecursively(currentNodeVo, aggregationNode, nodeIdMapNode, nodeList,
                        bpmnStartConditionsVo);
                }

                RecursionTreate(nodeVo, nodeIdMapNode, nodeList, bpmnStartConditionsVo);
            }
        }
    }

    private void RecursionTreate(BpmnNodeVo node, Dictionary<String, BpmnNodeVo> nodeIdMapNode,
        List<BpmnNodeVo> filterNodeList, BpmnStartConditionsVo bpmnStartConditionsVo)
    {
        if (ObjectUtils.IsEmpty(node.NodeTo))
        {
            node.Params = new BpmnNodeParamsVo();
            filterNodeList.Add(node);
            return;
        }

        //to prevent from endless recursion
        if (filterNodeList.Count > nodeIdMapNode.Count)
        {
            _logger.LogInformation("nodeId error,nodeMap:{}", JsonSerializer.Serialize(nodeIdMapNode));
            throw new AFBizException("999", "Node's nodeId config error");
        }

        BpmnNodeVo nextNode = null;
        BpmnNodeParamsVo parameters = new BpmnNodeParamsVo();

        if ((int)NodeTypeEnum.NODE_TYPE_GATEWAY != node.NodeType)
        {
            BpmnNodeVo next = nodeIdMapNode[node.NodeTo[0]];
            if (next == null)
            {
                _logger.LogError("can not find out process's next node,nodeId:{},nextNodeId:{}", node.NodeId,
                    node.NodeTo[0]);
                throw new AFBizException("999", "can not find out process's next node");
            }

            parameters.NodeTo = ObjectUtils.IsEmpty(node.NodeTo) ? null : node.NodeTo[0];
            node.Params = parameters;
            filterNodeList.Add(node);
            nextNode = next;
        }
        else
        {
            List<BpmnNodeVo> beforeFilterNodeList = new List<BpmnNodeVo>();
            node.NodeTo.ForEach(o => beforeFilterNodeList.Add(nodeIdMapNode[o]));
            if (beforeFilterNodeList.Count != node.NodeTo.Count())
            {
                _logger.LogError("wrong number of conditions node,nodeId:{}", node.NodeId);
                throw new AFBizException("999", "wrong number of conditions node,nodeId");
            }

            //determine next node by condition
            BpmnNodeVo afterFilterNode = FilterCondition(beforeFilterNodeList, bpmnStartConditionsVo);
            if (afterFilterNode == null)
            {
                throw new AFBizException("999",
                    "had no branch that meet the given condition,please contat the Administrator！");
            }

            parameters.NodeTo = afterFilterNode.NodeId;
            node.Params = parameters;
            filterNodeList.Add(node);
            nextNode = afterFilterNode;
        }
        //recursionTreate(nextNode, nodeIdMapNode, filterNodeList, bpmnStartConditionsVo);
    }
    public List<BpmnNodeVo> DeleteConditionNode(List<BpmnNodeVo> nodeList)
    {
        var notConditionNodeMap = new Dictionary<string, BpmnNodeVo>();
        var conditionNodeMap = new Dictionary<string, BpmnNodeVo>();

        foreach (var node in nodeList)
        {
            if (node.NodeType == (int)NodeTypeEnum.NODE_TYPE_GATEWAY || node.NodeType == (int)NodeTypeEnum.NODE_TYPE_CONDITIONS)
            {
                conditionNodeMap[node.NodeId] = node;
            }
            else
            {
                notConditionNodeMap[node.NodeId] = node;
            }
        }

        var resultList = new List<BpmnNodeVo>();

        foreach (var entry in notConditionNodeMap)
        {
            var currentNode = entry.Value;
            var nextNodeId = currentNode.Params?.NodeTo;

            if (string.IsNullOrWhiteSpace(nextNodeId))
            {
                resultList.Add(currentNode);
                continue;
            }

            // Next node is a condition node or assignee node
            if (!notConditionNodeMap.ContainsKey(nextNodeId))
            {
                var resultNodeId = FindNext(nextNodeId, new List<string>(), notConditionNodeMap, conditionNodeMap);

                if (!string.IsNullOrWhiteSpace(resultNodeId))
                {
                    var resultNode = notConditionNodeMap[resultNodeId];
                    resultNode.NodeFrom = currentNode.NodeId;
                }

                currentNode.Params.NodeTo = resultNodeId;
            }

            resultList.Add(currentNode);
        }

        return resultList;
    }
    private string FindNext(string nodeId, List<string> addNodeIdList, Dictionary<string, BpmnNodeVo> notConditionNodeMap, Dictionary<string, BpmnNodeVo> conditionNodeMap)
    {
        if (notConditionNodeMap.ContainsKey(nodeId) || string.IsNullOrWhiteSpace(nodeId))
        {
            return nodeId;
        }

        // Solve endless recursion
        if (addNodeIdList.Contains(nodeId))
        {
            _logger.LogInformation("Node ID forms a cycle, nodeId: {NodeId}", nodeId);
            throw new AFBizException("999", "Node ID forms a cycle");
        }

        if (!conditionNodeMap.TryGetValue(nodeId, out var bpmnNodeVo))
        {
            _logger.LogInformation("Has no node ID, nodeId: {NodeId}", nodeId);
            throw new AFBizException("999", "Has no node ID");
        }

        addNodeIdList.Add(nodeId);
        return FindNext(bpmnNodeVo.Params.NodeTo, addNodeIdList, notConditionNodeMap, conditionNodeMap);
    }

    public BpmnNodeVo FilterCondition(List<BpmnNodeVo> beforeFilterNodeList,
        BpmnStartConditionsVo bpmnStartConditionsVo)
    {
        if (beforeFilterNodeList == null || !beforeFilterNodeList.Any())
        {
            _logger.LogInformation("Condition nodes are empty");
            return null;
        }

        // Get a list of condition nodes, excluding default ones
        var filterNodeList = beforeFilterNodeList
            .Where(o => o.Property.ConditionsConf.IsDefault == 0 && o.Property.ConditionsConf.Sort.HasValue)
            .OrderByDescending(o => o.Property.ConditionsConf.Sort.Value)
            .ToList();

        // Iterate the nodes to check whether it meets all the given conditions
        foreach (var bpmnNodeVo in filterNodeList)
        {
            if ((int)NodeTypeEnum.NODE_TYPE_CONDITIONS != bpmnNodeVo.NodeType)
            {
                _logger.LogInformation(
                    "Gateway's next node, but not a condition node, continue. NodeId: {NodeId}, NodeType: {NodeType}",
                    bpmnNodeVo.NodeId, bpmnNodeVo.NodeType);
                continue;
            }

            // Check whether the node meets the given condition
            bool matchCondition = _conditionService.CheckMatchCondition(
                bpmnNodeVo.NodeId,
                bpmnNodeVo.Property.ConditionsConf,
                bpmnStartConditionsVo);

            if (!matchCondition)
            {
                continue;
            }

            // If multiple nodes meet the conditions, only the first one will return
            return bpmnNodeVo;
        }

        // Has no node that meets the given conditions, then choose the default one
        var defaultConditionNodes = beforeFilterNodeList
            .Where(o => o.Property.ConditionsConf.IsDefault == 1)
            .ToList();

        if (defaultConditionNodes.Any())
        {
            return defaultConditionNodes.First();
        }

        // Default behavior: No conditions matched, return null
        return null;
    }
    private void CheckNode(List<BpmnNodeVo> list) {
        if (ObjectUtils.IsEmpty(list)) {
            return;
        }
        int nodeCustomize = 0;
        foreach (BpmnNodeVo bpmnNodeVo in list) {
            if ((int)NodeTypeEnum.NODE_TYPE_APPROVER==bpmnNodeVo.NodeType
                && (int)NodePropertyEnum.NODE_PROPERTY_CUSTOMIZE==bpmnNodeVo.NodeProperty) {
                nodeCustomize += 1;
                if (nodeCustomize > 1) {
                    throw new AFBizException("self chose module is greater than 1");
                }
            }
        }
    }
    private BpmnNodeVo GetNodeMapAndStartNode(List<BpmnNodeVo> nodeList, Dictionary<String, BpmnNodeVo> nodeIdMapNode)
    {
        BpmnNodeVo startNode = null;
        foreach (BpmnNodeVo bpmnNodeVo in nodeList)
        {
            nodeIdMapNode.Add(bpmnNodeVo.NodeId, bpmnNodeVo);
            if ((int)NodeTypeEnum.NODE_TYPE_START == bpmnNodeVo.NodeType)
            {
                if (startNode == null)
                {
                    startNode = bpmnNodeVo;
                }
                else
                {
                    _logger.LogError("multiple start user,nodeId:{}", bpmnNodeVo.NodeId);
                    throw new AFBizException("999", "process has multiple start user");
                }
            }
        }

        return startNode;
    }
}