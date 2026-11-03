# adds a line to ~/log/raids, raid.cron runs it when enemies show up
# cat ~/log/raids shows the log

if not exists(~/log)
  run mkdir ~/log
end
line = "day " + /colony/day + ", " + /colony/hour + "h, " + /danger/enemies + " enemies"
append ~/log/raids = line
