while true do
    if economy.wood >= 100 then
        buildHarvester()
    end
    coroutine.yield()
end
