from PythonAI.ambusher.reservations import ReservationTable


def test_reservations_are_exclusive_until_expiry():
    table = ReservationTable()
    assert table.reserve("cover", "one", 0, 2)
    assert not table.reserve("cover", "two", 1, 2)
    assert table.reserve("cover", "two", 2, 2)


def test_owner_reassignment_releases_previous_point():
    table = ReservationTable()
    assert table.reserve("alley", "one", 0, 5)
    assert table.reserve("rooftop", "one", 1, 5)
    assert table.is_available("alley", "two", 1)
    assert not table.is_available("rooftop", "two", 1)


def test_release_clears_every_reservation_owned_by_agent():
    table = ReservationTable()
    assert table.reserve("cover", "two", 0, 3)
    table.release("two")
    assert table.is_available("cover", "one", 1)


def test_non_positive_lifetime_is_rejected():
    table = ReservationTable()
    assert not table.reserve("cover", "one", 0, 0)
    assert not table.reserve("cover", "one", 0, -1)

