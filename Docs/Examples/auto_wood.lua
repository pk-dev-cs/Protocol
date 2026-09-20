while true do
    local tree = robot.findNearestTree()
    if tree ~= nil then
        robot.moveTo(tree)
        robot.chop(tree)
        robot.returnToBase()
        robot.depositResources()
    else
        robot.wait(1)
    end
end
