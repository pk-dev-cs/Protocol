while true do
    local mine = robot.findNearestMine()
    if mine ~= nil then
        robot.moveTo(mine)
        robot.mine(mine)
        robot.returnToBase()
        robot.depositResources()
        print(unitName .. ': dostarczono iron')
    else
        robot.wait(1)
    end
end

