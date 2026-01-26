The reactive component system is built around the idea of reaction nodes (for the user, these are Game Objects),
which contain actions (eg - toggle an object on or off), and which can themselves be toggled on or off by either
actions attached to other reaction nodes, or by external animations and conditions.

Reactions that are associated with each other (ie, which can be triggered by the same external event) are grouped into
reaction groups, and the externally visible effects of each group all are applied at the same time. This requires
calculating the path delay through the final generated animation graph, and inserting dummy parameters to delay the
final effects (or an intermediate edge).

When we start out, we have a bag of targets (things which can be acted upon), and a list of actions (in priority order)
for each target. We then take these through a series of transformations to generate the final graph.

1. First, we construct a reaction node for each target/action pair.
2. For ObjectActiveState expressions where an external animation drives the state, we introduce a parameter that will
   be substituted into the external animations, then add a ParameterCondition to these nodes.
    - This creates an OrNode(And(ParameterCondition, ObjectActiveState(NotDriven)), ObjectActiveState(true/false))
3. Optimization pass: We eliminate ObjectActiveState expressions which are not driven by any reaction node, then
   simplify the resulting
   expressions.
    1. As part of simplification, we remove redundant constant expressions, single-element AND/OR expressions, etc.
4. Next, we group reaction nodes into reaction groups. Two nodes are in the same group if one of the following is true:
    1. They contain the same (non-constant) ObjectActiveState target, or an ObjectActiveDriver node for that target
    2. They contain a ParameterCondition for the same parameter name
    3. They have the same target for their action
5. Optimization pass: We perform action forwarding, as follows: For each node whose expression is dependent on a
   single ObjectActiveState target; if there is a single (or a small number of nodes) driving that ObjectActiveState,
   we can replace that ObjectActiveState expression with a boolean condition built from the expressions of the driving
   node.
6. Optimization pass: We perform another round of expression simplification.
7. We convert all ObjectActiveState expressions into InternalParameterConditions, and all ObjectActiveDriver nodes into
   InternalParameterDrivers.
8. We check for loops in this graph; if we find one, we break the loop (by replacing the InternalParameterDrivers with
   normal ParameterDrivers/ParameterConditions).
9. We construct PriorityGroups for each target, then group together targets with the same PriorityGroups. However,
   we do not group InternalParameterDrivers (we keep each as an independent PriorityGroup)
10. We now assign a depth to each PriorityGroup, with all PriorityGroups that contain non-InternalParameterDrivers at
   depth 0. The depth of the node depends on its type; a priority group with multiple conditions requires a delay of
   2, while others have 1.
11. We add DelayNodes to the graph to align all nodes at the same point. This is done as follows:
   1. Start by assigning all nodes with external effects to depth 0, and adding them to a visit queue. Assign -1 to all
      other nodes.
   2. While the visit queue is not empty:
      1. Pop a node from the visit queue
      2. For each of its input nodes:
         1. If the input node depth is smaller than the current node depth + delay, set the input node depth to current
            node depth + delay,
            and add it to the queue (if it isn't already)
   3. Now visit all nodes; if they have inputs that are not at the depth given by the current node depth + delay,
      insert DelayNodes to adjust the delays appropriately (each DelayNode effectively subtracts one unit of delay)
12. Finally, we convert the priority nodes to a motion node PriorityNode or BranchNode.